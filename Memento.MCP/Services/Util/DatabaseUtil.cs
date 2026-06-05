using System.Data;
using System.Text.Json;

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

    // ── 表操作 ──

    public static string QualifyTable(string tableName, string? databaseName, string dbType) =>
        databaseName != null ? $"{QuoteName(databaseName, dbType)}.{QuoteName(tableName, dbType)}" : QuoteName(tableName, dbType);

    /// <summary>获取建表 SQL</summary>
    public static string CreateTableSql(string dbType, string tableName, List<ColumnDef> columns, string? databaseName = null)
    {
        var q = QualifyTable(tableName, databaseName, dbType);
        var cols = columns.Select(c =>
        {
            var parts = new List<string> { QuoteName(c.Name, dbType), c.Type };
            if (c.AutoIncrement) parts.Add("AUTO_INCREMENT");
            if (!c.Nullable) parts.Add("NOT NULL");
            if (c.DefaultValue != null) parts.Add($"DEFAULT {c.DefaultValue}");
            if (c.PrimaryKey) parts.Add("PRIMARY KEY");
            if (c.Comment != null && dbType.ToLowerInvariant() == "mysql")
                parts.Add($"COMMENT '{c.Comment}'");
            return string.Join(" ", parts);
        });
        return $"CREATE TABLE {q} (\n  {string.Join(",\n  ", cols)}\n)";
    }

    /// <summary>获取删除表的 SQL</summary>
    public static string DropTableSql(string dbType, string tableName, string? databaseName = null) =>
        $"DROP TABLE {QualifyTable(tableName, databaseName, dbType)}";

    /// <summary>获取清空表的 SQL</summary>
    public static string TruncateTableSql(string dbType, string tableName, string? databaseName = null) =>
        $"TRUNCATE TABLE {QualifyTable(tableName, databaseName, dbType)}";

    /// <summary>获取添加字段的 SQL</summary>
    public static string AlterTableAddColumnSql(string dbType, string tableName, ColumnDef column, string? databaseName = null)
    {
        var q = QualifyTable(tableName, databaseName, dbType);
        var parts = new List<string> { "ADD", QuoteName(column.Name, dbType), column.Type };
        if (!column.Nullable) parts.Add("NOT NULL");
        if (column.DefaultValue != null) parts.Add($"DEFAULT {column.DefaultValue}");
        if (column.Comment != null && dbType.ToLowerInvariant() == "mysql")
            parts.Add($"COMMENT '{column.Comment}'");
        return $"ALTER TABLE {q} {string.Join(" ", parts)}";
    }

    /// <summary>获取删除字段的 SQL</summary>
    public static string AlterTableDropColumnSql(string dbType, string tableName, string columnName, string? databaseName = null) =>
        $"ALTER TABLE {QualifyTable(tableName, databaseName, dbType)} DROP {QuoteName(columnName, dbType)}";

    /// <summary>获取修改字段的 SQL</summary>
    public static string AlterTableModifyColumnSql(string dbType, string tableName, ColumnDef column, string? databaseName = null)
    {
        var q = QualifyTable(tableName, databaseName, dbType);
        var sql = dbType.ToLowerInvariant() switch
        {
            "mysql" => $"ALTER TABLE {q} MODIFY COLUMN ",
            _ => $"ALTER TABLE {q} ALTER COLUMN ",
        };
        var parts = new List<string> { QuoteName(column.Name, dbType), column.Type };
        if (!column.Nullable) parts.Add("NOT NULL");
        if (column.DefaultValue != null) parts.Add($"DEFAULT {column.DefaultValue}");
        return sql + string.Join(" ", parts);
    }

    /// <summary>获取描述表结构的 SQL</summary>
    public static string DescribeTableSql(string dbType, string tableName, string databaseName)
    {
        var q = QualifyTable(tableName, databaseName, dbType);
        return dbType.ToLowerInvariant() switch
        {
            "mysql" => $"DESCRIBE {q}",
            "postgresql" or "postgres" => $"SELECT column_name, data_type, is_nullable, column_default FROM information_schema.columns WHERE table_schema = '{databaseName}' AND table_name = '{tableName}'",
            "sqlserver" => $"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_CATALOG = '{databaseName}' AND TABLE_NAME = '{tableName}'",
            _ => throw new ArgumentException($"不支持的数据库类型: {dbType}"),
        };
    }

    /// <summary>获取列出所有表的 SQL，支持模糊查询</summary>
    public static string ListTablesSql(string dbType, string databaseName, string? pattern = null)
    {
        var sql = dbType.ToLowerInvariant() switch
        {
            "mysql" => $"SELECT TABLE_NAME AS name FROM information_schema.tables WHERE TABLE_SCHEMA = '{databaseName}' AND TABLE_TYPE = 'BASE TABLE'",
            "postgresql" or "postgres" => $"SELECT tablename AS name FROM pg_catalog.pg_tables WHERE schemaname = '{databaseName}'",
            "sqlserver" => $"SELECT TABLE_NAME AS name FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{databaseName}' AND TABLE_TYPE = 'BASE TABLE'",
            _ => throw new ArgumentException($"不支持的数据库类型: {dbType}"),
        };
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            var nameCol = dbType.ToLowerInvariant() switch
            {
                "mysql" => "TABLE_NAME",
                "postgresql" or "postgres" => "tablename",
                "sqlserver" => "TABLE_NAME",
                _ => "name",
            };
            sql += $" AND {nameCol} LIKE '%{pattern}%'";
        }
        sql += " ORDER BY name";
        return sql;
    }

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

    // ── 数据操作 ──

    /// <summary>将值转为 SQL 字面量（自动处理引号和转义）</summary>
    public static string ValueToSql(object? value, string dbType)
    {
        if (value == null || value == DBNull.Value) return "NULL";
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Null => "NULL",
                JsonValueKind.String => $"'{je.GetString()?.Replace("'", "''")}'",
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                JsonValueKind.Number => je.GetRawText(),
                _ => throw new ArgumentException($"不支持的值类型: {je.ValueKind}"),
            };
        }
        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            int or long or short or byte or float or double or decimal => value.ToString()!,
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => $"'{value.ToString()?.Replace("'", "''")}'",
        };
    }

    /// <summary>生成 INSERT SQL（支持多行批量插入）</summary>
    public static string InsertSql(string dbType, string tableName, List<Dictionary<string, object?>> rows, string? databaseName = null)
    {
        if (rows.Count == 0) throw new ArgumentException("至少需要一行数据");
        var q = QualifyTable(tableName, databaseName, dbType);
        var columns = rows[0].Keys.ToList();
        var colNames = string.Join(", ", columns.Select(c => QuoteName(c, dbType)));
        var values = rows.Select(row =>
        {
            var vals = columns.Select(col => row.TryGetValue(col, out var v) ? ValueToSql(v, dbType) : "NULL");
            return $"({string.Join(", ", vals)})";
        });
        return $"INSERT INTO {q} ({colNames}) VALUES {string.Join(", ", values)}";
    }

    /// <summary>生成 UPDATE SQL</summary>
    public static string UpdateSql(string dbType, string tableName, Dictionary<string, object?> data, string where, string? databaseName = null)
    {
        if (data.Count == 0) throw new ArgumentException("至少需要一个要更新的字段");
        var q = QualifyTable(tableName, databaseName, dbType);
        var setClause = string.Join(", ", data.Select(kv => $"{QuoteName(kv.Key, dbType)} = {ValueToSql(kv.Value, dbType)}"));
        var sql = $"UPDATE {q} SET {setClause}";
        if (!string.IsNullOrWhiteSpace(where)) sql += $" WHERE {where}";
        return sql;
    }

    /// <summary>生成 DELETE SQL</summary>
    public static string DeleteSql(string dbType, string tableName, string where, string? databaseName = null)
    {
        var q = QualifyTable(tableName, databaseName, dbType);
        var sql = $"DELETE FROM {q}";
        if (!string.IsNullOrWhiteSpace(where)) sql += $" WHERE {where}";
        return sql;
    }
}
