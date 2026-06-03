namespace Memento.MCP.Register.Attributes;

/// <summary>描述 MCP 工具参数或 DTO 属性的 JSON Schema 信息</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public class McpParamAttribute : Attribute
{
    /// <summary>参数描述</summary>
    public string Description { get; }

    /// <summary>参数默认值（有默认值的参数会自动标记为非必须）</summary>
    public object? DefaultValue { get; set; }

    public McpParamAttribute(string description)
    {
        Description = description;
    }
}
