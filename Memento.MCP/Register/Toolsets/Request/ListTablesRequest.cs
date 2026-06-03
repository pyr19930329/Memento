using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class ListTablesRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名")]
    public string DatabaseName { get; set; } = "";

    [McpParam("模糊搜索关键字（可选，如不传则列出所有表）")]
    public string? Pattern { get; set; }
}
