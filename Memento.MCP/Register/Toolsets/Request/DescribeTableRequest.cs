using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class DescribeTableRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名")]
    public string DatabaseName { get; set; } = "";

    [McpParam("表名")]
    public string TableName { get; set; } = "";
}
