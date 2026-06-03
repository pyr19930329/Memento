using McpDotNet.Protocol.Types;

namespace Memento.MCP.Services.Util;

/// <summary>MCP 工具响应的快捷构造</summary>
public static class ToolResponse
{
    /// <summary>成功响应</summary>
    public static CallToolResponse Ok(string text) => new()
    {
        Content = [new() { Text = text, Type = "text" }],
    };

    /// <summary>错误响应</summary>
    public static CallToolResponse Error(string msg) => new()
    {
        Content = [new() { Text = $"[ERR] {msg}", Type = "text" }],
        IsError = true,
    };

    /// <summary>按布尔值返回成功/错误响应</summary>
    public static CallToolResponse Result(bool ok, string msg) =>
        ok ? Ok($"[OK] {msg}") : Error(msg);
}
