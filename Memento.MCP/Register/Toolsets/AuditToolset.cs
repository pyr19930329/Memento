using System.ComponentModel;
using System.Text.Json;
using Memento.MCP.Services.Util;
using ModelContextProtocol.Server;

namespace Memento.MCP.Register.Toolsets;

public class AuditToolset
{
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [McpServerTool, Description("查询指定表的所有审计日志（新增/删除/修改/查询）")]
    public Task<string> AuditByTable(
        [Description("已保存的连接名称")] string connection_name,
        [Description("要查询的表名（支持模糊匹配）")] string table_name,
        [Description("跳过条数，默认 0")] int skip = 0,
        [Description("返回条数，默认 100")] int take = 100)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return Task.FromResult("[ERR] 连接名称不能为空");
        if (string.IsNullOrWhiteSpace(table_name)) return Task.FromResult("[ERR] 表名不能为空");

        var entries = AuditLogger.QueryByTable(connection_name, table_name);
        var result = entries.Skip(skip).Take(take).ToList();
        var json = JsonSerializer.Serialize(result, JsonOpts);
        return Task.FromResult($"共 {entries.Count} 条，显示 {result.Count} 条\n{json}");
    }

    [McpServerTool, Description("查看最近的审计日志")]
    public Task<string> AuditRecent(
        [Description("已保存的连接名称")] string connection_name,
        [Description("跳过条数，默认 0")] int skip = 0,
        [Description("返回条数，默认 20")] int take = 20)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return Task.FromResult("[ERR] 连接名称不能为空");

        var entries = AuditLogger.GetAll(connection_name, skip, take);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        return Task.FromResult($"共返回 {entries.Count} 条\n{json}");
    }
}
