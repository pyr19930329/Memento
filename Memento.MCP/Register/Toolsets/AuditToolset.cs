using System.Text.Json;
using Memento.MCP.Register.Attributes;
using Memento.MCP.Services.Util;

namespace Memento.MCP.Register.Toolsets;

[McpToolset]
public class AuditToolset
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Tool(Name = "audit_by_table", Description = "查询指定表的所有审计日志（新增/删除/修改/查询）")]
    public CallToolResponse AuditByTable(
        [McpParam("已保存的连接名称")] string connection_name,
        [McpParam("要查询的表名（支持模糊匹配）")] string table_name,
        [McpParam("跳过条数，默认 0")] int skip = 0,
        [McpParam("返回条数，默认 100")] int take = 100)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return ToolResponse.Error("连接名称不能为空");
        if (string.IsNullOrWhiteSpace(table_name)) return ToolResponse.Error("表名不能为空");

        var entries = AuditLogger.QueryByTable(connection_name, table_name);
        var result = entries.Skip(skip).Take(take).ToList();
        var json = JsonSerializer.Serialize(result, JsonOpts);
        return ToolResponse.Ok($"共 {entries.Count} 条，显示 {result.Count} 条\n{json}");
    }

    [Tool(Name = "audit_recent", Description = "查看最近的审计日志")]
    public CallToolResponse AuditRecent(
        [McpParam("已保存的连接名称")] string connection_name,
        [McpParam("跳过条数，默认 0")] int skip = 0,
        [McpParam("返回条数，默认 20")] int take = 20)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return ToolResponse.Error("连接名称不能为空");

        var entries = AuditLogger.GetAll(connection_name, skip, take);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        return ToolResponse.Ok($"共返回 {entries.Count} 条\n{json}");
    }
}
