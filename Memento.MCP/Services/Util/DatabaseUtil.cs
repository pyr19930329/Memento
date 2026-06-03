using System.Data;

namespace Memento.MCP.Services.Util;

/// <summary>数据库操作工具 — 标识符引号、SQL 生成、DataTable 转换</summary>
public static class DatabaseUtil
{
    /// <summary>按数据库类型给标识符加引号</summary>
    public static string QuoteName(string name, string dbType) => dbType.ToLowerInvariant() switch
    {
        "mysql" => $"`{name}`",
        "postgresql" or "postgres" => $"\"{name}\"",
        "sqlserver" => $"[{name}]",
        _ => name,
    };

    /// <summary>获取列出所有数据库的 SQL</summary>
    public static string ListDatabasesSql(string dbType) => dbType.ToLowerInvariant() switch
    {
        "mysql" => "SHOW DATABASES",
        "postgresql" or "postgres" => "SELECT datname AS name FROM pg_database ORDER BY datname",
        "sqlserver" => "SELECT name FROM sys.databases ORDER BY name",
        _ => throw new ArgumentException($"不支持的数据库类型: {dbType}"),
    };

    /// <summary>获取重命名数据库的 SQL</summary>
    public static string AlterDatabaseSql(string dbType, string oldName, string newName)
    {
        var q = QuoteName(oldName, dbType);
        var qNew = QuoteName(newName, dbType);
        return dbType.ToLowerInvariant() switch
        {
            "mysql" => throw new NotSupportedException("MySQL 5.1 起已移除 RENAME DATABASE，请使用 execute 工具手动执行：\n" +
                $"1. CREATE DATABASE {qNew};\n" +
                $"2. 用 RENAME TABLE 迁移各表到新库\n" +
                $"3. DROP DATABASE {q};"),
            "postgresql" => $"ALTER DATABASE {q} RENAME TO {qNew}",
            "sqlserver" => $"ALTER DATABASE {q} MODIFY NAME = {qNew}",
            _ => throw new ArgumentException($"不支持的数据库类型: {dbType}"),
        };
    }

    /// <summary>获取创建数据库的 SQL</summary>
    public static string CreateDatabaseSql(string dbType, string name, string charset = "utf8")
    {
        var q = QuoteName(name, dbType);
        return dbType.ToLowerInvariant() switch
        {
            "mysql" => $"CREATE DATABASE {q} CHARACTER SET {charset}",
            "postgresql" or "postgres" => $"CREATE DATABASE {q} ENCODING 'UTF8'",
            "sqlserver" => $"CREATE DATABASE {q}",
            _ => throw new ArgumentException($"不支持的数据库类型: {dbType}"),
        };
    }

    /// <summary>获取删除数据库的 SQL</summary>
    public static string DropDatabaseSql(string dbType, string name) =>
        $"DROP DATABASE {QuoteName(name, dbType)}";

    /// <summary>将 DataTable 转为字典列表（用于 JSON 序列化）</summary>
    public static List<Dictionary<string, object?>> DataTableToList(DataTable dt)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in dt.Rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns)
                dict[col.ColumnName] = row[col];
            rows.Add(dict);
        }
        return rows;
    }
}
