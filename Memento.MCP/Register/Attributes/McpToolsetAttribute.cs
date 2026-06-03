namespace Memento.MCP.Register.Attributes;

/// <summary>标记一个类为 MCP 工具集，其中的 [McpTool] 方法会被自动注册</summary>
[AttributeUsage(AttributeTargets.Class)]
public class McpToolsetAttribute : Attribute;
