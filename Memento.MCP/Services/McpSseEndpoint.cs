using System.Text.Json;
using Memento.MCP.Register;

namespace Memento.MCP.Services;

/// <summary>MCP Streamable HTTP / SSE 端点处理</summary>
public static class McpSseEndpoint
{
    private static readonly JsonSerializerOptions JsonOpts = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>在 ASP.NET Core 应用上注册 MCP SSE 端点</summary>
    public static void Map(WebApplication app, McpToolRegistry registry)
    {
        var caps = registry.BuildCapabilities();

        app.MapGet("/mcp", async (HttpContext ctx) => {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            // 保持 SSE 连接活跃（客户端通过此通道接收服务器消息）
            // 客户端发送的请求通过 POST /mcp 提交
            await ctx.Response.WriteAsync($"event: endpoint\ndata: /mcp\n\n");
            await ctx.Response.Body.FlushAsync();

            // 保持连接直到客户端断开
            try
            {
                await Task.Delay(Timeout.Infinite, ctx.RequestAborted);
            }
            catch (OperationCanceledException) { }
        });

        app.MapPost("/mcp", async (HttpContext ctx) => {
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOpts);
                if (request == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("Invalid JSON-RPC request");
                    return;
                }

                var response = await HandleRequest(request, caps);

                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOpts));
            }
            catch (JsonException ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = (string?)null,
                    error = new { code = -32700, message = $"Parse error: {ex.Message}" },
                }, JsonOpts));
            }
        });
    }

    private static async Task<object> HandleRequest(JsonRpcRequest req, ServerCapabilities caps)
    {
        var id = req.Id;

        switch (req.Method)
        {
            case "initialize":
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "Memento.MCP", version = "1.0.0" },
                        instructions = "",
                    },
                };

            case "tools/list":
            {
                var result = await caps.Tools!.ListToolsHandler();
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new { tools = result.Tools },
                };
            }

            case "tools/call":
            {
                if (req.Params is null)
                    return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Missing params" } };
                var p = req.Params.Value;
                var name = p.GetProperty("name").GetString();
                var args = p.GetProperty("arguments");

                var argDict = new Dictionary<string, object?>();
                if (args.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in args.EnumerateObject())
                        argDict[ToCamelCase(prop.Name)] = ConvertJsonElement(prop.Value);
                }

                var response = await caps.Tools!.CallToolHandler(name ?? "", argDict);
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        content = response.Content.Select(c => new { type = c.Type, text = c.Text }).ToList(),
                        isError = response.IsError,
                    },
                };
            }

            case "notifications/initialized":
                // 通知无需响应
                return new { };

            default:
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    error = new { code = -32601, message = $"Method not found: {req.Method}" },
                };
        }
    }

    private static object? ConvertJsonElement(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array or JsonValueKind.Object => el,  // 保留原始 JsonElement，避免转成字符串
            _ => el.GetRawText(),
        };
    }

    private static string ToCamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

internal class JsonRpcRequest
{
    public JsonElement? Id { get; set; }
    public string? Method { get; set; }
    public JsonElement? Params { get; set; }
}
