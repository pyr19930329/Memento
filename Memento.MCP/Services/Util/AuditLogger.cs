using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memento.MCP.Services.Util;

/// <summary>SQL 执行审计日志 — 按连接名写入独立 JSON 文件</summary>
public static partial class AuditLogger
{
    private static readonly string LogDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly SemaphoreSlim Lock = new(1, 1);

    // ── 操作分类 ──

    public enum Level { Database, Table, Column, Data }
    public enum Operation { Create, Query, Update, Delete, Alter }

    /// <summary>记录一条审计日志</summary>
    public static void Record(string connectionName, string sql, long durationMs)
    {
        var entry = Parse(sql);
        entry.ConnectionName = connectionName;
        entry.DurationMs = durationMs;

        _ = WriteAsync(connectionName, entry);
    }

    /// <summary>按表名查询审计日志</summary>
    public static List<AuditEntry> QueryByTable(string connectionName, string tableName)
    {
        var file = LogFilePath(connectionName);
        if (!File.Exists(file)) return [];

        try
        {
            var lines = File.ReadAllLines(file);
            return lines
                .Select(l => JsonSerializer.Deserialize<AuditEntry>(l))
                .Where(e => e != null && e.Table != null &&
                    e.Table.Contains(tableName, StringComparison.OrdinalIgnoreCase))
                .Select(e => e!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>获取所有审计日志（支持分页）</summary>
    public static List<AuditEntry> GetAll(string connectionName, int skip = 0, int take = 100)
    {
        var file = LogFilePath(connectionName);
        if (!File.Exists(file)) return [];

        try
        {
            return File.ReadAllLines(file)
                .Skip(skip).Take(take)
                .Select(l => JsonSerializer.Deserialize<AuditEntry>(l))
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    // ── 内部 ──

    private static string LogFilePath(string connectionName) =>
        Path.Combine(LogDir, $"{SanitizeFileName(connectionName)}_audit.jsonl");

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));

    private static async Task WriteAsync(string connectionName, AuditEntry entry)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = JsonSerializer.Serialize(entry, JsonOpts);
            await Lock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(LogFilePath(connectionName), line + "\n");
            }
            finally
            {
                Lock.Release();
            }
        }
        catch
        {
            // 审计日志写入失败不影响主流程
        }
    }

    /// <summary>从 SQL 中解析操作类型、层级和目标表名</summary>
    private static AuditEntry Parse(string sql)
    {
        var trimmed = sql.TrimStart().TrimEnd(';');
        var entry = new AuditEntry
        {
            Timestamp = DateTimeOffset.Now.ToString("o"),
            Sql = sql,
        };

        // 按首个单词判断操作类型
        var firstWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "";

        entry.Operation = firstWord switch
        {
            "SELECT" or "SHOW" or "DESCRIBE" or "DESC" => Operation.Query.ToString(),
            "INSERT" or "CREATE" => Operation.Create.ToString(),
            "UPDATE" => Operation.Update.ToString(),
            "DELETE" or "DROP" or "TRUNCATE" => Operation.Delete.ToString(),
            "ALTER" or "RENAME" => Operation.Alter.ToString(),
            _ => Operation.Query.ToString(),
        };

        // 按关键词判断层级
        entry.Level = firstWord switch
        {
            "CREATE" or "DROP" or "ALTER" or "RENAME" => trimmed.Contains("DATABASE", StringComparison.OrdinalIgnoreCase)
                ? Level.Database.ToString()
                : trimmed.Contains("TABLE", StringComparison.OrdinalIgnoreCase)
                    ? Level.Table.ToString()
                    : Level.Database.ToString(),
            "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "TRUNCATE" => Level.Data.ToString(),
            "SHOW" or "DESCRIBE" or "DESC" => Level.Table.ToString(),
            _ => Level.Data.ToString(),
        };

        // 提取表名
        if (entry.Level == Level.Data.ToString() || entry.Level == Level.Table.ToString())
        {
            entry.Table = ExtractTableName(trimmed, firstWord);
        }
        else if (entry.Level == Level.Column.ToString())
        {
            entry.Table = ExtractTableName(trimmed, "ALTER");
        }
        else if (entry.Level == Level.Database.ToString())
        {
            entry.Database = ExtractDatabaseName(trimmed, firstWord);
        }

        return entry;
    }

    private static string? ExtractTableName(string sql, string firstWord)
    {
        // 正则匹配: FROM table, INTO table, UPDATE table, TABLE table, JOIN table
        string[] patterns;

        switch (firstWord)
        {
            case "SELECT":
                patterns = [@"\bFROM\s+[`""'\[\]]?(\w+)[`""'\[\]]?"];
                break;
            case "INSERT":
                patterns = [@"\bINTO\s+[`""'\[\]]?(\w+)[`""'\[\]]?"];
                break;
            case "UPDATE":
                patterns = [@"\bUPDATE\s+[`""'\[\]]?(\w+)[`""'\[\]]?"];
                break;
            case "DELETE":
                patterns = [@"\bFROM\s+[`""'\[\]]?(\w+)[`""'\[\]]?"];
                break;
            case "ALTER":
            case "CREATE":
            case "DROP":
            case "TRUNCATE":
                patterns = [@"\bTABLE\s+[`""'\[\]]?(\w+)[`""'\[\]]?"];
                break;
            case "SHOW":
            case "DESCRIBE":
            case "DESC":
                patterns = [@"\b(\w+)\s*$"];
                break;
            default:
                patterns = [@"\b(\w+)\s*$"];
                break;
        }

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    private static string? ExtractDatabaseName(string sql, string firstWord)
    {
        var match = Regex.Match(sql, @"\bDATABASE\s+[`""'\[\]]?(\w+)[`""'\[\]]?", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}

/// <summary>审计日志条目</summary>
public class AuditEntry
{
    public string Timestamp { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public string? Database { get; set; }
    public string? Table { get; set; }
    public string Level { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Sql { get; set; } = "";
    public long DurationMs { get; set; }
}
