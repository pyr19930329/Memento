using System.Text.Json;
using Memento.MCP.Register.Attributes;
using Memento.MCP.Register.Toolsets.Request;
using Memento.MCP.Services.Util;

namespace Memento.MCP.Register.Toolsets;

[McpToolset]
public class DatabaseToolset
{
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Tool(Name = "list_databases", Description = "列出数据库服务器上的所有数据库")]
    public CallToolResponse ListDatabases([McpParam("已保存的连接名称")] string connection_name) {
        
        if (string.IsNullOrWhiteSpace(connection_name)) return ToolResponse.Error("连接名称不能为空");

        var db = SqlSugarUtil.GetClientByName(connection_name);
        var sql = DatabaseUtil.ListDatabasesSql(db.CurrentConnectionConfig.DbType.ToString());
        var names = db.Ado.GetDataTable(sql).Rows.Cast<System.Data.DataRow>().Select(r => r[0]?.ToString() ?? "").OrderBy(n => n).ToList();
        return ToolResponse.Ok(JsonSerializer.Serialize(names, JsonOpts));
    }

    [Tool(Name = "create_database", Description = "创建新数据库")]
    public CallToolResponse CreateDatabase([McpBody] CreateDatabaseRequest req) {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名称不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var charset = req.Charset ?? "utf8";
        var sql = DatabaseUtil.CreateDatabaseSql(dbType, req.DatabaseName, charset);
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"数据库 '{req.DatabaseName}' 已创建 (charset={charset})");
    }

    [Tool(Name = "alter_database", Description = "修改数据库（重命名）")]
    public CallToolResponse AlterDatabase([McpBody] AlterDatabaseRequest req) {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("当前数据库名称不能为空");
        if (string.IsNullOrWhiteSpace(req.NewDatabaseName)) return ToolResponse.Error("新数据库名称不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();

        if (dbType.ToLowerInvariant() == "mysql")
        {
            // MySQL 5.1+ 已移除 RENAME DATABASE，改用建新库→迁移表→删旧库
            var tables = db.Ado.GetDataTable(
                $"SELECT TABLE_NAME FROM information_schema.tables WHERE TABLE_SCHEMA = '{req.DatabaseName}' AND TABLE_TYPE = 'BASE TABLE'");
            var tableNames = tables.Rows.Cast<System.Data.DataRow>().Select(r => r[0]?.ToString()).Where(n => n != null).Cast<string>().ToList();

            var qOld = DatabaseUtil.QuoteName(req.DatabaseName, dbType);
            var qNew = DatabaseUtil.QuoteName(req.NewDatabaseName, dbType);

            db.Ado.ExecuteCommand($"CREATE DATABASE {qNew}");
            foreach (var t in tableNames)
            {
                var qT = DatabaseUtil.QuoteName(t, dbType);
                db.Ado.ExecuteCommand($"RENAME TABLE {qOld}.{qT} TO {qNew}.{qT}");
            }
            db.Ado.ExecuteCommand($"DROP DATABASE {qOld}");

            return ToolResponse.Ok($"数据库 '{req.DatabaseName}' 已重命名为 '{req.NewDatabaseName}'（迁移 {tableNames.Count} 张表）");
        }

        var sql = DatabaseUtil.AlterDatabaseSql(dbType, req.DatabaseName, req.NewDatabaseName);
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"数据库 '{req.DatabaseName}' 已重命名为 '{req.NewDatabaseName}'");
    }

    [Tool(Name = "drop_database", Description = "删除数据库（危险操作！）")]
    public CallToolResponse DropDatabase([McpParam("已保存的连接名称")] string connection_name, [McpParam("要删除的数据库名称")] string database_name) {
        if (string.IsNullOrWhiteSpace(connection_name)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(database_name)) return ToolResponse.Error("数据库名称不能为空");
        var db = SqlSugarUtil.GetClientByName(connection_name);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.DropDatabaseSql(dbType, database_name);
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"数据库 '{database_name}' 已删除");
    }

    [Tool(Name = "query", Description = "执行 SQL 查询（SELECT），返回 JSON 结果")]
    public CallToolResponse Query([McpParam("已保存的连接名称")] string connection_name, [McpParam("SQL 查询语句")] string sql) {
        if (string.IsNullOrWhiteSpace(connection_name)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(sql)) return ToolResponse.Error("SQL 语句不能为空");
        var db = SqlSugarUtil.GetClientByName(connection_name);
        var dt = db.Ado.GetDataTable(sql);
        var rows = DatabaseUtil.DataTableToList(dt);
        return ToolResponse.Ok(JsonSerializer.Serialize(rows, JsonOpts));
    }

    [Tool(Name = "execute", Description = "执行 SQL 命令（INSERT/UPDATE/DELETE/CREATE/ALTER/DROP），返回影响行数")]
    public CallToolResponse Execute([McpParam("已保存的连接名称")] string connection_name, [McpParam("SQL 命令语句")] string sql) {
        if (string.IsNullOrWhiteSpace(connection_name)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(sql)) return ToolResponse.Error("SQL 语句不能为空");
        var db = SqlSugarUtil.GetClientByName(connection_name);
        var affected = db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"影响行数: {affected}");
    }
}
