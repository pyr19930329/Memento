using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class AlterDatabaseRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("当前数据库名称")]
    public string DatabaseName { get; set; } = "";

    [McpParam("新数据库名称")]
    public string NewDatabaseName { get; set; } = "";
}
