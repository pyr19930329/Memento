using System.ComponentModel;
using System.Text.Json;
using Memento.MCP.Services.Util;
using ModelContextProtocol.Server;

namespace Memento.MCP.Register.Toolsets;

public class BackupToolset
{
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [McpServerTool, Description("列出所有备份记录，可选按连接名过滤")]
    public Task<string> ListBackups([Description("连接的名称（可选，不传则列出全部）")] string? connection_name = null)
    {
        var list = BackupUtil.ListBackups(connection_name);
        if (list.Count == 0) return Task.FromResult("暂无备份记录");
        return Task.FromResult(JsonSerializer.Serialize(list, JsonOpts));
    }

    [McpServerTool, Description("根据备份 GUID 恢复数据（INSERT IGNORE），支持跨表恢复")]
    public async Task<string> RestoreByGuid([Description("已保存的连接名称")] string connection_name, [Description("备份 GUID")] string guid, [Description("数据库名")] string database_name)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(guid)) return "[ERR] GUID 不能为空";

        var record = BackupUtil.GetBackup(guid, connection_name);
        if (record == null) return $"[ERR] 未找到备份: {guid}";

        var db = SqlSugarUtil.GetClientByName(connection_name);
        return await BackupUtil.RestoreAsync(db, database_name, record);
    }

    [McpServerTool, Description("根据表名恢复最近一次备份")]
    public async Task<string> RestoreByTable([Description("已保存的连接名称")] string connection_name, [Description("表名")] string table_name, [Description("数据库名")] string database_name)
    {
        if (string.IsNullOrWhiteSpace(connection_name)) return "[ERR] 连接名称不能为空";
        if (string.IsNullOrWhiteSpace(table_name)) return "[ERR] 表名不能为空";

        var backups = BackupUtil.ListBackups(connection_name)
            .Where(b => b.TableName.Equals(table_name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (backups.Count == 0) return $"[ERR] 未找到表 '{table_name}' 的备份记录";

        var record = backups.First();
        var db = SqlSugarUtil.GetClientByName(connection_name);
        return await BackupUtil.RestoreAsync(db, database_name, record);
    }
}
