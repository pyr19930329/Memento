using Memento.MCP.Register.Attributes;
using Memento.MCP.Services.Util;

namespace Memento.MCP.Register.Toolsets.Request;

public class AlterTableRequest
{
    [McpParam("已保存的连接名称")]
    public string ConnectionName { get; set; } = "";

    [McpParam("数据库名（可选，不传则用连接默认库）")]
    public string? DatabaseName { get; set; }

    [McpParam("表名")]
    public string TableName { get; set; } = "";

    [McpParam("操作类型：add / drop / modify")]
    public string Operation { get; set; } = "add";

    [McpParam("字段名（drop/add/modify 时用）")]
    public string? ColumnName { get; set; }

    [McpParam("字段类型（add/modify 时用，如 varchar(255)）")]
    public string? ColumnType { get; set; }

    [McpParam("是否可为空（add/modify 时用）")]
    public bool? Nullable { get; set; }

    [McpParam("默认值（add/modify 时用）")]
    public string? DefaultValue { get; set; }
}
