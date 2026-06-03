using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class AddConnectionRequest {
    [McpParam("连接名称（唯一标识）")]
    public string Name { get; set; } = "";

    [McpParam("数据库连接字符串")]
    public string ConnectionString { get; set; } = "";

    [McpParam("数据库类型：MySql / SqlServer / PostgreSQL，默认 MySql")]
    public string? DbType { get; set; }
}
