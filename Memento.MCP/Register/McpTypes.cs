namespace Memento.MCP.Register;

/// <summary>MCP 工具调用响应</summary>
public class CallToolResponse
{
    public List<Content> Content { get; set; } = [];
    public bool IsError { get; set; }
}

/// <summary>MCP 响应内容块</summary>
public class Content
{
    public string Type { get; set; } = "text";
    public string Text { get; set; } = "";
}

/// <summary>JSON Schema 属性定义（对应 MCP 协议中的 inputSchema.properties）</summary>
public class JsonSchemaProperty
{
    public string Type { get; set; } = "string";
    public string Description { get; set; } = "";
}

/// <summary>tools/list 响应</summary>
public class ListToolsResult
{
    public List<ToolDef> Tools { get; set; } = [];
}

/// <summary>tools/list 中的工具条目</summary>
public class ToolDef
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonSchema InputSchema { get; set; } = new();
}

/// <summary>JSON Schema 定义</summary>
public class JsonSchema
{
    public string Type { get; set; } = "object";
    public Dictionary<string, JsonSchemaProperty> Properties { get; set; } = [];
    public List<string> Required { get; set; } = [];
}

/// <summary>服务端能力</summary>
public class ServerCapabilities
{
    public ToolCapabilities? Tools { get; set; }
}

/// <summary>工具能力</summary>
public class ToolCapabilities
{
    public Func<Task<ListToolsResult>> ListToolsHandler { get; set; } = () => Task.FromResult(new ListToolsResult());
    public Func<string, Dictionary<string, object?>?, Task<CallToolResponse>> CallToolHandler { get; set; } = (_, _) => Task.FromResult(new CallToolResponse());
}
