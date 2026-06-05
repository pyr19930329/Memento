using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets.Request;

public class InsertDataRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名（可空）")]
    public string? DatabaseName { get; set; }

    [McpParam("表名")]
    public string TableName { get; set; } = "";

    [McpParam("要插入的数据行列表，每行是一个字段名→值的字典")]
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}
