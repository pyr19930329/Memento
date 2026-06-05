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
    public static void Record(string connectionName, string sql, long durationMs, string? guid = null, string? backupFile = null)
    {
        var entry = Parse(sql);
        entry.ConnectionName = connectionName;
        entry.DurationMs = durationMs;
        entry.Guid = guid;
        entry.BackupFile = backupFile;

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
            (entry.Database, entry.Table) = ExtractTableName(trimmed, firstWord);
        }
        else if (entry.Level == Level.Column.ToString())
        {
            (entry.Database, entry.Table) = ExtractTableName(trimmed, "ALTER");
        }
        else if (entry.Level == Level.Database.ToString())
        {
            entry.Database = ExtractDatabaseName(trimmed, firstWord);
        }

        return entry;
    }

    /// <summary>从 SQL 中提取数据库名和表名。返回 (database, table)，database 可能为 null</summary>
    private static (string? Database, string? Table) ExtractTableName(string sql, string firstWord)
    {
        // 关键字后提取表引用的正则模式
        string keywordPattern = firstWord switch
        {
            "SELECT"    => @"\bFROM\s+",
            "INSERT"    => @"\bINTO\s+",
            "UPDATE"    => @"\bUPDATE\s+",
            "DELETE"    => @"\bFROM\s+",
            "ALTER" or "CREATE" or "DROP" or "TRUNCATE" => @"\bTABLE\s+",
            "SHOW" or "DESCRIBE" or "DESC" => @"\b(\w+)\s*$",
            _ => @"\b(\w+)\s*$",
        };

        // 对 SHOW/DESCRIBE 直接取最后一个词
        if (firstWord is "SHOW" or "DESCRIBE" or "DESC")
        {
            var m = Regex.Match(sql, keywordPattern, RegexOptions.IgnoreCase);
            return m.Success ? (null, m.Groups[1].Value) : (null, null);
        }

        var match = Regex.Match(sql, keywordPattern, RegexOptions.IgnoreCase);
        if (!match.Success) return (null, null);

        var after = sql.Substring(match.Index + match.Length).TrimStart();

        // 匹配 db.table 或 table 形式，自动跳过引号
        // 引号集: ` (backtick), " (double), ' (single), [ (bracket)
        var refMatch = Regex.Match(after,
            @"^[`""'\[\]]?(\w+)[`""'\[\]]?" +                            // 第一段
            @"(?:\.\s*[`""'\[\]]?(\w+)[`""'\[\]]?)?");                    // 可选 .第二段
        if (!refMatch.Success) return (null, null);

        var part1 = refMatch.Groups[1].Value;
        var part2 = refMatch.Groups[2].Value;

        if (string.IsNullOrEmpty(part2))
        {
            // 只有 tableName
            return (null, part1);
        }
        else
        {
            // database.table
            return (part1, part2);
        }
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
    public string? Guid { get; set; }
    public string Timestamp { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public string? Database { get; set; }
    public string? Table { get; set; }
    public string Level { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Sql { get; set; } = "";
    public long DurationMs { get; set; }
    public string? BackupFile { get; set; }
}
