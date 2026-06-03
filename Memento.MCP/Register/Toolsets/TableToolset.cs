using System.Text.Json;
using McpDotNet.Protocol.Types;
using Memento.MCP.Register.Attributes;
using Memento.MCP.Register.Toolsets.Request;
using Memento.MCP.Services.Util;

namespace Memento.MCP.Register.Toolsets;

[McpToolset]
public class TableToolset
{
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Tool(Name = "describe_table", Description = "查看表字段结构")]
    public CallToolResponse DescribeTable([McpBody] DescribeTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名不能为空");
        if (string.IsNullOrWhiteSpace(req.TableName)) return ToolResponse.Error("表名不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.DescribeTableSql(dbType, req.TableName, req.DatabaseName);
        var dt = db.Ado.GetDataTable(sql);
        var rows = DatabaseUtil.DataTableToList(dt);
        return ToolResponse.Ok(JsonSerializer.Serialize(rows, JsonOpts));
    }

    [Tool(Name = "list_tables", Description = "列出数据库中的所有表，支持模糊搜索")]
    public CallToolResponse ListTables([McpBody] ListTablesRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.ListTablesSql(dbType, req.DatabaseName, req.Pattern);
        var dt = db.Ado.GetDataTable(sql);
        var names = dt.Rows.Cast<System.Data.DataRow>().Select(r => r["name"]?.ToString() ?? "").ToList();
        return ToolResponse.Ok(JsonSerializer.Serialize(names, JsonOpts));
    }

    [Tool(Name = "create_table", Description = "创建新表")]
    public CallToolResponse CreateTable([McpBody] CreateTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名不能为空");
        if (string.IsNullOrWhiteSpace(req.TableName)) return ToolResponse.Error("表名不能为空");
        if (req.Columns == null || req.Columns.Count == 0) return ToolResponse.Error("至少需要一个字段");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.CreateTableSql(dbType, req.TableName, req.Columns, req.DatabaseName);
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"表 '{req.DatabaseName}.{req.TableName}' 已创建（{req.Columns.Count} 个字段）");
    }

    [Tool(Name = "alter_table", Description = "修改表结构（add / drop / modify 字段）")]
    public CallToolResponse AlterTable([McpBody] AlterTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名不能为空");
        if (string.IsNullOrWhiteSpace(req.TableName)) return ToolResponse.Error("表名不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();

        string sql = req.Operation.ToLowerInvariant() switch
        {
            "add" => DatabaseUtil.AlterTableAddColumnSql(dbType, req.TableName, new ColumnDef
            {
                Name = req.ColumnName ?? "",
                Type = req.ColumnType ?? "varchar(255)",
                Nullable = req.Nullable ?? true,
                DefaultValue = req.DefaultValue,
            }, req.DatabaseName),
            "drop" => DatabaseUtil.AlterTableDropColumnSql(dbType, req.TableName, req.ColumnName ?? "", req.DatabaseName),
            "modify" => DatabaseUtil.AlterTableModifyColumnSql(dbType, req.TableName, new ColumnDef
            {
                Name = req.ColumnName ?? "",
                Type = req.ColumnType ?? "varchar(255)",
                Nullable = req.Nullable ?? true,
                DefaultValue = req.DefaultValue,
            }, req.DatabaseName),
            _ => throw new ArgumentException($"不支持的操作: {req.Operation}（支持 add / drop / modify）"),
        };
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"表 '{req.DatabaseName}.{req.TableName}' 已修改（{req.Operation} {req.ColumnName}）");
    }

    [Tool(Name = "drop_table", Description = "删除表")]
    public CallToolResponse DropTable([McpBody] DropTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名不能为空");
        if (string.IsNullOrWhiteSpace(req.TableName)) return ToolResponse.Error("表名不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.DropTableSql(dbType, req.TableName, req.DatabaseName);
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"表 '{req.DatabaseName}.{req.TableName}' 已删除");
    }

    [Tool(Name = "truncate_table", Description = "清空表数据（保留表结构）")]
    public CallToolResponse TruncateTable([McpBody] TruncateTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionName)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(req.DatabaseName)) return ToolResponse.Error("数据库名不能为空");
        if (string.IsNullOrWhiteSpace(req.TableName)) return ToolResponse.Error("表名不能为空");
        var db = SqlSugarUtil.GetClientByName(req.ConnectionName);
        var dbType = db.CurrentConnectionConfig.DbType.ToString();
        var sql = DatabaseUtil.TruncateTableSql(dbType, req.TableName, req.DatabaseName);
        db.Ado.ExecuteCommand(sql);
        return ToolResponse.Ok($"表 '{req.DatabaseName}.{req.TableName}' 已清空");
    }
}
