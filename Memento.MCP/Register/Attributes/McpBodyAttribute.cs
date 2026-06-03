namespace Memento.MCP.Register.Attributes;

/// <summary>
/// 将 arguments JSON 整体反序列化为参数类型的实例，类似 ASP.NET Core 的 [FromBody]。
/// 标记的参数类型必须是 class，其公开属性自动成为 JSON Schema。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class McpBodyAttribute : Attribute;
