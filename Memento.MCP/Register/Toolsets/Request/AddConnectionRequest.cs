using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class AddConnectionRequest
{
    [McpParam("连接名称（唯一标识）")]
    public string Name { get; set; } = "";

    [McpParam("数据库主机地址（IP 或域名）")]
    public string Host { get; set; } = "";

    [McpParam("数据库端口")]
    public int Port { get; set; } = 3306;

    [McpParam("用户名")]
    public string UserId { get; set; } = "";

    [McpParam("密码")]
    public string Password { get; set; } = "";

    [McpParam("数据库类型：MySql / SqlServer / PostgreSQL，默认 MySql")]
    public string? DbType { get; set; }
}

