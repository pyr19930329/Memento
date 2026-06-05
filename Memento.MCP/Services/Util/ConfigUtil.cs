using System.Text.Json;

namespace Memento.MCP.Services.Util;

/// <summary>应用配置辅助，从 appsettings.json 读取配置项</summary>
public static class ConfigUtil
{
    /// <summary>
    /// 从 appsettings.json 读取指定配置项的布尔值（支持点号嵌套路径，如 "Backup.AlterTable"）
    /// </summary>
    /// <param name="key">配置键名，支持点号分隔的嵌套路径</param>
    /// <param name="defaultValue">键不存在或解析失败时的默认值</param>
    public static bool GetAppConfigBool(string key, bool defaultValue = false)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return defaultValue;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var element = doc.RootElement;

            // 支持点号分隔的嵌套路径，如 "Backup.AlterTable"
            var parts = key.Split('.');
            foreach (var part in parts)
            {
                if (element.ValueKind != JsonValueKind.Object) return defaultValue;
                if (!element.TryGetProperty(part, out element)) return defaultValue;
            }

            return element.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return defaultValue;
        }
    }
}
