using System.ComponentModel;
using System.Text.Json;
using Memento.MCP.Register.Toolsets.Request;
using Memento.MCP.Services.Util;
using ModelContextProtocol.Server;

namespace Memento.MCP.Register.Toolsets;

public class TableToolset {
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, };

    [McpServerTool, Description("查看表字段结构")]
    public async Task<string> DescribeTable([Description("查看表字段参数")] DescribeTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名不能为空";
        if (string.IsNullOrWhiteSpace(req.TableName)) return "[ERR] 表名不能为空";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.DescribeTableSql(dbType, req.TableName, req.DatabaseName);
        var dt = await db.Ado.GetDataTableAsync(sql);
        var rows = DatabaseUtil.DataTableToList(dt);
        return JsonSerializer.Serialize(rows, JsonOpts);
    }

    [McpServerTool, Description("列出数据库中的所有表，支持模糊搜索")]
    public async Task<string> ListTables([Description("列表参数")] ListTablesRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名不能为空";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.ListTablesSql(dbType, req.DatabaseName, req.Pattern);
        var dt = await db.Ado.GetDataTableAsync(sql);
        var names = dt.Rows.Cast<System.Data.DataRow>().Select(r => r["name"]?.ToString() ?? "").ToList();
        return JsonSerializer.Serialize(names, JsonOpts);
    }

    [McpServerTool, Description("创建新表")]
    public async Task<string> CreateTable([Description("建表参数")] CreateTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名不能为空";
        if (string.IsNullOrWhiteSpace(req.TableName)) return "[ERR] 表名不能为空";
        if (req.Columns == null || req.Columns.Count == 0) return "[ERR] 至少需要一个字段";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.CreateTableSql(dbType, req.TableName, req.Columns, req.DatabaseName);
        await db.Ado.ExecuteCommandAsync(sql);
        return $"表 '{req.DatabaseName}.{req.TableName}' 已创建（{req.Columns.Count} 个字段）";
    }

    [McpServerTool, Description("修改表结构（add / drop / modify 字段）")]
    public async Task<string> AlterTable(McpServer server, CancellationToken cancellationToken, [Description("修改表参数")] AlterTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名不能为空";
        if (string.IsNullOrWhiteSpace(req.TableName)) return "[ERR] 表名不能为空";
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();

        var op = req.Operation.ToLowerInvariant();
        if (op == "drop")
        {
            if (!await ElicitUtil.ConfirmAsync(server, $"⚠️ 将删除表 '{req.DatabaseName}.{req.TableName}' 的字段 '{req.ColumnName}'，数据不可恢复！是否确认？", cancellationToken))
                return "[ERR] 操作已取消";
        }

        var sql = op switch {
            "add" => DatabaseUtil.AlterTableAddColumnSql(dbType, req.TableName, new ColumnDef {
                Name = req.ColumnName ?? "",
                Type = req.ColumnType ?? "varchar(255)",
                Nullable = req.Nullable ?? true,
                DefaultValue = req.DefaultValue,
            }, req.DatabaseName),
            "drop" => DatabaseUtil.AlterTableDropColumnSql(dbType, req.TableName, req.ColumnName ?? "", req.DatabaseName),
            "modify" => DatabaseUtil.AlterTableModifyColumnSql(dbType, req.TableName, new ColumnDef {
                Name = req.ColumnName ?? "",
                Type = req.ColumnType ?? "varchar(255)",
                Nullable = req.Nullable ?? true,
                DefaultValue = req.DefaultValue,
            }, req.DatabaseName),
            _ => $"[ERR] 不支持的操作: {op}（支持 add / drop / modify）",
        };
        await db.Ado.ExecuteCommandAsync(sql);
        return $"表 '{req.DatabaseName}.{req.TableName}' 已修改（{op} {req.ColumnName}）";
    }

    [McpServerTool, Description("删除表")]
    public async Task<string> DropTable(McpServer server, CancellationToken cancellationToken, [Description("删表参数")] DropTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名不能为空";
        if (string.IsNullOrWhiteSpace(req.TableName)) return "[ERR] 表名不能为空";

        if (!await ElicitUtil.ConfirmAsync(server,
                $"⚠️ 将永久删除表 '{req.DatabaseName}.{req.TableName}'，数据不可恢复！是否确认？",
                cancellationToken))
            return "[ERR] 操作已取消";

        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.DropTableSql(dbType, req.TableName, req.DatabaseName);
        await db.Ado.ExecuteCommandAsync(sql);
        return $"表 '{req.DatabaseName}.{req.TableName}' 已删除";
    }

    [McpServerTool, Description("清空表数据（保留表结构）")]
    public async Task<string> TruncateTable(McpServer server, CancellationToken cancellationToken, [Description("清空表参数")] TruncateTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return "[ERR] 数据库名不能为空";
        if (string.IsNullOrWhiteSpace(req.TableName)) return "[ERR] 表名不能为空";

        if (!await ElicitUtil.ConfirmAsync(server,
                $"⚠️ 将清空表 '{req.DatabaseName}.{req.TableName}' 所有数据，不可恢复！是否确认？",
                cancellationToken))
            return "[ERR] 操作已取消";

        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.TruncateTableSql(dbType, req.TableName, req.DatabaseName);
        await db.Ado.ExecuteCommandAsync(sql);
        return $"表 '{req.DatabaseName}.{req.TableName}' 已清空";
    }
}
