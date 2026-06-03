using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class UpdateConnectionRequest
{
    [McpParam("要修改的连接名称")]
    public string Name { get; set; } = "";

    [McpParam("新名称（可选）")]
    public string? NewName { get; set; }

    [McpParam("新连接字符串（可选）")]
    public string? ConnectionString { get; set; }

    [McpParam("新数据库类型（可选）")]
    public string? DbType { get; set; }
}
