using System.Text.Json;
using SqlSugar;

namespace Memento.MCP.Services.Util;

/// <summary>数据库备份工具 — 在危险操作前备份数据到 JSON 文件，通过 GUID 关联审计日志</summary>
public static class BackupUtil
{
    private static readonly string BackupDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs", "backups");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>生成全局唯一 ID</summary>
    public static string NewGuid() => Guid.NewGuid().ToString("N");

    /// <summary>备份指定表的全表数据，返回备份文件路径</summary>
    public static async Task<string> BackupTableAsync(ISqlSugarClient db, string databaseName, string tableName, string guid)
    {
        var rows = await db.Ado.GetDataTableAsync($"SELECT * FROM {Quote(tableName, databaseName)}");
        return await WriteBackupAsync(guid, new BackupRecord
        {
            Guid = guid,
            ConnectionName = db.CurrentConnectionConfig.ConfigId?.ToString() ?? "",
            TableName = tableName,
            Operation = "backup_table",
            BackupTime = DateTimeOffset.Now.ToString("o"),
            Columns = rows.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList(),
            Rows = DataTableToDictList(rows),
        });
    }

    /// <summary>备份指定字段的数据，返回备份文件路径</summary>
    public static async Task<string> BackupColumnAsync(ISqlSugarClient db, string databaseName, string tableName, string columnName, string guid)
    {
        var rows = await db.Ado.GetDataTableAsync($"SELECT * FROM {Quote(tableName, databaseName)}");
        return await WriteBackupAsync(guid, new BackupRecord
        {
            Guid = guid,
            ConnectionName = db.CurrentConnectionConfig.ConfigId?.ToString() ?? "",
            TableName = tableName,
            Operation = "backup_column",
            ColumnName = columnName,
            BackupTime = DateTimeOffset.Now.ToString("o"),
            Columns = rows.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList(),
            Rows = DataTableToDictList(rows),
        });
    }

    /// <summary>按条件备份指定表的数据，返回备份文件路径</summary>
    /// <param name="where">WHERE 条件（不含 WHERE 关键字），为空时备份全表</param>
    public static async Task<string> BackupTableWithWhereAsync(ISqlSugarClient db, string databaseName, string tableName, string? where, string guid)
    {
        var sql = $"SELECT * FROM {Quote(tableName, databaseName)}";
        if (!string.IsNullOrWhiteSpace(where)) sql += $" WHERE {where}";
        var rows = await db.Ado.GetDataTableAsync(sql);
        return await WriteBackupAsync(guid, new BackupRecord
        {
            Guid = guid,
            ConnectionName = db.CurrentConnectionConfig.ConfigId?.ToString() ?? "",
            TableName = tableName,
            Operation = "backup_where",
            BackupTime = DateTimeOffset.Now.ToString("o"),
            Columns = rows.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList(),
            Rows = DataTableToDictList(rows),
        });
    }

    /// <summary>备份数据库所有表，返回备份文件路径列表</summary>
    public static async Task<List<string>> BackupDatabaseAsync(ISqlSugarClient db, string databaseName, string guid, string dbType)
    {
        var prefix = dbType.ToLowerInvariant() == "mysql" ? $"{databaseName}." : "";
        var tables = await db.Ado.GetDataTableAsync(
            $"SELECT TABLE_NAME FROM information_schema.tables WHERE TABLE_SCHEMA = '{databaseName}' AND TABLE_TYPE = 'BASE TABLE'");
        var paths = new List<string>();
        foreach (var row in tables.Rows.Cast<System.Data.DataRow>())
        {
            var t = row[0]?.ToString();
            if (string.IsNullOrWhiteSpace(t)) continue;
            var subGuid = NewGuid();
            var rows = await db.Ado.GetDataTableAsync($"SELECT * FROM {prefix}{Quote(t)}");
            var path = await WriteBackupAsync(subGuid, new BackupRecord
            {
                Guid = subGuid,
                ConnectionName = db.CurrentConnectionConfig.ConfigId?.ToString() ?? "",
                TableName = t,
                Operation = "backup_database",
                BackupTime = DateTimeOffset.Now.ToString("o"),
                Columns = rows.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList(),
                Rows = DataTableToDictList(rows),
            });
            paths.Add(path);
        }
        return paths;
    }

    // ── 恢复 ──

    /// <summary>列出所有备份记录（连接名过滤）</summary>
    public static List<BackupRecord> ListBackups(string? connectionName = null)
    {
        var dir = Path.Combine(BackupDir, Sanitize(connectionName ?? ""));
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.json")
            .Select(f =>
            {
                try
                {
                    var json = File.ReadAllText(f);
                    return JsonSerializer.Deserialize<BackupRecord>(json);
                }
                catch { return null; }
            })
            .Where(r => r != null)
            .Select(r => r!)
            .OrderByDescending(r => r.BackupTime)
            .ToList();
    }

    /// <summary>按 GUID 获取备份记录</summary>
    public static BackupRecord? GetBackup(string guid, string? connectionName = null)
    {
        if (connectionName != null)
        {
            var path = BackupFilePath(guid, connectionName);
            return File.Exists(path) ? ReadBackup(path) : null;
        }

        // 不指定连接名时遍历所有目录
        var dir = Path.Combine(BackupDir);
        if (!Directory.Exists(dir)) return null;
        foreach (var connDir in Directory.GetDirectories(dir))
        {
            var path = Path.Combine(connDir, $"{guid}.json");
            if (File.Exists(path)) return ReadBackup(path);
        }
        return null;
    }

    /// <summary>从备份记录恢复数据（INSERT IGNORE）</summary>
    public static async Task<string> RestoreAsync(ISqlSugarClient db, string databaseName, BackupRecord record)
    {
        if (record.Rows.Count == 0) return "备份记录无数据";

        var tableName = record.TableName;
        var columns = string.Join(", ", record.Columns.Select(c => QuoteName(c)));
        var values = new List<string>();

        foreach (var row in record.Rows)
        {
            var vals = record.Columns.Select(c =>
            {
                if (!row.TryGetValue(c, out var val) || val == null) return "NULL";
                if (val is JsonElement je)
                    return je.ValueKind == JsonValueKind.Null ? "NULL"
                        : je.ValueKind == JsonValueKind.String ? $"'{EscapeSql(je.GetString()!)}'"
                        : je.GetRawText();
                return $"'{EscapeSql(val.ToString()!)}'";
            });
            values.Add($"({string.Join(", ", vals)})");
        }

        var sql = $"INSERT IGNORE INTO {Quote(tableName, databaseName)} ({columns}) VALUES\n{string.Join(",\n", values)}";
        var affected = await db.Ado.ExecuteCommandAsync(sql);
        return $"已恢复 {affected} 条记录到表 '{databaseName}.{tableName}'";
    }

    // ── 内部 ──

    private static string Quote(string name, string? database = null)
    {
        var q = $"`{name}`";
        return database != null ? $"`{database}`.{q}" : q;
    }

    private static string QuoteName(string name) => $"`{name}`";

    private static string EscapeSql(string s) => s.Replace("'", "''").Replace("\\", "\\\\");

    private static string Sanitize(string name) =>
        string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));

    private static string BackupDirFor(string connectionName) =>
        Path.Combine(BackupDir, Sanitize(connectionName));

    private static string BackupFilePath(string guid, string connectionName) =>
        Path.Combine(BackupDirFor(connectionName), $"{guid}.json");

    private static BackupRecord? ReadBackup(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BackupRecord>(json);
        }
        catch { return null; }
    }

    private static async Task<string> WriteBackupAsync(string guid, BackupRecord record)
    {
        var connName = record.ConnectionName;
        var dir = BackupDirFor(connName);
        Directory.CreateDirectory(dir);
        var path = BackupFilePath(guid, connName);
        var json = JsonSerializer.Serialize(record, JsonOpts);
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static List<Dictionary<string, object?>> DataTableToDictList(System.Data.DataTable dt)
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (System.Data.DataRow row in dt.Rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (System.Data.DataColumn col in dt.Columns)
                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            result.Add(dict);
        }
        return result;
    }
}

/// <summary>备份记录</summary>
public class BackupRecord
{
    public string Guid { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? ColumnName { get; set; }
    public string Operation { get; set; } = "";
    public string BackupTime { get; set; } = "";
    public List<string> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}
