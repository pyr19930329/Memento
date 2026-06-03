namespace Memento.MCP.Register.Attributes;

/// <summary>标记方法为 MCP 工具，自动从方法签名生成 Schema</summary>
[AttributeUsage(AttributeTargets.Method)]
public class ToolAttribute : Attribute
{
    /// <summary>工具名称，默认使用方法名</summary>
    public string? Name { get; set; }

    /// <summary>工具描述</summary>
    public string? Description { get; set; }
}
