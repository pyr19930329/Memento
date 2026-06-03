using System.Text.Json.Serialization;
using Memento.MCP.Register.Attributes;
using Memento.MCP.Services.Util;

namespace Memento.MCP.Register.Toolsets.Request;

public class CreateTableRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名")]
    public string? DatabaseName { get; set; }

    [McpParam("表名")]
    public string TableName { get; set; } = "";

    [McpParam("字段定义数组")]
    public List<ColumnDef> Columns { get; set; } = [];
}
