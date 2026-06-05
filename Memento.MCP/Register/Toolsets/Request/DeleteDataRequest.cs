using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class DeleteDataRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名（可空）")]
    public string? DatabaseName { get; set; }

    [McpParam("表名")]
    public string TableName { get; set; } = "";

    [McpParam("WHERE 条件（不含 WHERE 关键字），例如 id = 1")]
    public string Where { get; set; } = "";
}
