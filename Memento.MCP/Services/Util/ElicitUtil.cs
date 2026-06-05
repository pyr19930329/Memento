using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Memento.MCP.Services.Util;

/// <summary>Elicitation 用户确认辅助</summary>
public static class ElicitUtil
{
    /// <summary>向客户端发起危险操作确认弹窗，客户端不支持或用户取消返回 false</summary>
    public static async Task<bool> ConfirmAsync(McpServer server, string message, CancellationToken ct)
    {
        // ── 检查配置：appsettings.json 中 Elicitation 必须为 true ──
        if (!IsElicitationEnabled()) {
            await Console.Error.WriteLineAsync("[ELICIT] Elicitation disabled by appsettings.json");
            return false;
        }

        var supported = server.ClientCapabilities?.Elicitation != null;
        await Console.Error.WriteLineAsync($"[ELICIT] Elicitation supported by client: {supported}");
        await Console.Error.WriteLineAsync($"[ELICIT] ClientCapabilities null: {server.ClientCapabilities == null}");
        if (!supported) return false;

        try
        {
            var result = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = message,
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        ["confirm"] = new ElicitRequestParams.BooleanSchema
                        {
                            Description = "我确认执行此危险操作",
                            Default = false,
                        }
                    }
                }
            }, ct);

            var accepted = result.Action == "accept"
                && result.Content?.TryGetValue("confirm", out var val) == true
                && val.ValueKind == System.Text.Json.JsonValueKind.True;
            await Console.Error.WriteLineAsync($"[ELICIT] Result: action={result.Action}, accepted={accepted}");
            return accepted;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ELICIT] Exception: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool IsElicitationEnabled()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return false;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("Elicitation", out var val)
                && val.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }
}
