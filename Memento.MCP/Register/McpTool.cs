using McpDotNet.Protocol.Types;

namespace Memento.MCP.Register;

/// <summary>
/// MCP 工具定义 — 名称、描述、输入 Schema、执行逻辑
/// </summary>
public class McpTool
{
    /// <summary>工具名称（客户端调用时使用）</summary>
    public string Name { get; init; } = "";

    /// <summary>工具描述</summary>
    public string Description { get; init; } = "";

    /// <summary>输入参数 Schema 的属性字典</summary>
    public Dictionary<string, JsonSchemaProperty> Properties { get; init; } = [];

    /// <summary>必需的参数名列表</summary>
    public List<string> Required { get; init; } = [];

    /// <summary>执行委托：接收参数字典，返回工具调用响应</summary>
    public Func<Dictionary<string, object?>?, Task<CallToolResponse>> Handler { get; init; } = _ => Task.FromResult(new CallToolResponse { Content = [] });

    /// <summary>转换为 MCP 协议中的 Tool 对象（用于 tools/list 返回）</summary>
    public Tool ToTool() => new()
    {
        Name = Name,
        Description = Description,
        InputSchema = new JsonSchema {
            Type = "object",
            Properties = Properties,
            Required = Required,
        },
    };
}
