using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class UpdateConnectionRequest
{
    [McpParam("要修改的连接名称")]
    public string Name { get; set; } = "";

    [McpParam("新名称（可选）")]
    public string? NewName { get; set; }

    [McpParam("数据库主机地址（可选）")]
    public string? Host { get; set; }

    [McpParam("数据库端口（可选）")]
    public int? Port { get; set; }

    [McpParam("用户名（可选）")]
    public string? UserId { get; set; }

    [McpParam("密码（可选）")]
    public string? Password { get; set; }

    [McpParam("新数据库类型（可选）")]
    public string? DbType { get; set; }
}
