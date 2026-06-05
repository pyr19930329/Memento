using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class DropTableRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名")]
    public string DatabaseName { get; set; } = "";

    [McpParam("要删除的表名")]
    public string TableName { get; set; } = "";

    [McpParam("确认执行危险操作，必须为 true")]
    public bool? Confirm { get; set; }
}
