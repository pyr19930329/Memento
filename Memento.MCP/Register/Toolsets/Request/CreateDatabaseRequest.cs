using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class CreateDatabaseRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("要创建的数据库名称")]
    public string DatabaseName { get; set; } = "";

    [McpParam("字符集，默认 utf8")]
    public string? Charset { get; set; }
}
