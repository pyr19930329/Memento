using System.ComponentModel;
using System.Text.Json;
using Memento.MCP.Register.Toolsets.Request;
using Memento.MCP.Services.Util;
using ModelContextProtocol.Server;

namespace Memento.MCP.Register.Toolsets;

public class DatabaseToolset
{
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [McpServerTool, Description("列出数据库服务器上的所有数据库")]
    public async Task<string> ListDatabases([Description("已保存的连接名称")] string connection_name)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return "[ERR] 连接名称不能为空";
        var db = SqlSugarUtil.GetClientByName(connection_name);
        var sql = DatabaseUtil.ListDatabasesSql(db.CurrentConnectionConfig.DbType.ToString());
        var dt = await db.Ado.GetDataTableAsync(sql);
        var names = dt.Rows.Cast<System.Data.DataRow>().Select(r => r[0]?.ToString() ?? "").OrderBy(n => n).ToList();
        return JsonSerializer.Serialize(names, JsonOpts);
    }

    [McpServerTool, Description("创建新数据库")]
    public async Task<string> CreateDatabase([Description("创建数据库参数")] CreateDatabaseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名称不能为空";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var charset = req.Charset ?? "utf8";
        var sql = DatabaseUtil.CreateDatabaseSql(dbType, req.DatabaseName, charset);
        await db.Ado.ExecuteCommandAsync(sql);
        return $"数据库 '{req.DatabaseName}' 已创建 (charset={charset})";
    }

    [McpServerTool, Description("修改数据库（重命名）")]
    public async Task<string> AlterDatabase(McpServer server, CancellationToken cancellationToken, [Description("重命名数据库参数")] AlterDatabaseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 当前数据库名称不能为空";
        if (string.IsNullOrWhiteSpace(req.NewDatabaseName)) return "[ERR] 新数据库名称不能为空";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();

        if (dbType.ToLowerInvariant() == "mysql")
        {
            var tables = await db.Ado.GetDataTableAsync(
                $"SELECT TABLE_NAME FROM information_schema.tables WHERE TABLE_SCHEMA = '{req.DatabaseName}' AND TABLE_TYPE = 'BASE TABLE'");
            var count = tables.Rows.Count;
            var tableNames = tables.Rows.Cast<System.Data.DataRow>().Select(r => r[0]?.ToString()).Where(n => n != null).Cast<string>().ToList();

            if (!await ElicitUtil.ConfirmAsync(server, $"⚠️ 将重命名数据库 '{req.DatabaseName}' 为 '{req.NewDatabaseName}'（迁移 {count} 张表后删除旧库），是否确认？",
                    cancellationToken))
                return "[ERR] 操作已取消";

            var qOld = DatabaseUtil.QuoteName(req.DatabaseName, dbType);
            var qNew = DatabaseUtil.QuoteName(req.NewDatabaseName, dbType);

            await db.Ado.ExecuteCommandAsync($"CREATE DATABASE {qNew}");
            foreach (var t in tableNames)
            {
                var qT = DatabaseUtil.QuoteName(t, dbType);
                await db.Ado.ExecuteCommandAsync($"RENAME TABLE {qOld}.{qT} TO {qNew}.{qT}");
            }
            await db.Ado.ExecuteCommandAsync($"DROP DATABASE {qOld}");
            return $"数据库 '{req.DatabaseName}' 已重命名为 '{req.NewDatabaseName}'（迁移 {tableNames.Count} 张表）";
        }

        var sql = DatabaseUtil.AlterDatabaseSql(dbType, req.DatabaseName, req.NewDatabaseName);
        await db.Ado.ExecuteCommandAsync(sql);
        return $"数据库 '{req.DatabaseName}' 已重命名为 '{req.NewDatabaseName}'";
    }

    [McpServerTool, Description("删除数据库")]
    public async Task<string> DropDatabase(McpServer server, CancellationToken cancellationToken, [Description("删除数据库参数")] DropDatabaseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名称不能为空";
        if (!await ElicitUtil.ConfirmAsync(server, $"⚠️ 将永久删除数据库 '{req.DatabaseName}'，数据不可恢复！是否确认？", cancellationToken)) return "[ERR] 操作已取消";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.DropDatabaseSql(dbType, req.DatabaseName);
        await db.Ado.ExecuteCommandAsync(sql);
        return $"数据库 '{req.DatabaseName}' 已删除";
    }

    [McpServerTool, Description("执行 SQL 查询（SELECT），返回 JSON 结果")]
    public async Task<string> Query([Description("已保存的连接名称")] string connection_name, [Description("SQL 查询语句")] string sql)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(sql)) return "[ERR] SQL 语句不能为空";
        var db = SqlSugarUtil.GetClientByName(connection_name);
        var dt = await db.Ado.GetDataTableAsync(sql);
        var rows = DatabaseUtil.DataTableToList(dt);
        return JsonSerializer.Serialize(rows, JsonOpts);
    }

    [McpServerTool, Description("执行 SQL 命令（INSERT/UPDATE/DELETE/CREATE/ALTER/DROP），返回影响行数")]
    public async Task<string> Execute([Description("已保存的连接名称")] string connection_name, [Description("SQL 命令语句")] string sql)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(sql)) return "[ERR] SQL 语句不能为空";
        var db = SqlSugarUtil.GetClientByName(connection_name);
        var affected = await db.Ado.ExecuteCommandAsync(sql);
        return $"影响行数: {affected}";
    }
}
