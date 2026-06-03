using System.Text.Json;
using McpDotNet.Protocol.Types;
using Memento.MCP.Register.Attributes;
using Memento.MCP.Register.Toolsets.Request;
using Memento.MCP.Services;

namespace Memento.MCP.Register.Toolsets;

[McpToolset]
public class ConnectionToolset
{
    private readonly ConnectionManager _conn;
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ConnectionToolset(ConnectionManager conn) => _conn = conn;

    // ── 工具方法 ──
    [Tool(Name = "add_connection", Description = "添加数据库连接")]
    public CallToolResponse AddConnection([McpBody] AddConnectionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Error("连接名称不能为空");
        if (_conn.Get(req.Name) != null) return Error($"连接 '{req.Name}' 已存在");

        var dbType = req.DbType ?? "MySql";
        var (ok, msg) = _conn.Add(req.Name, req.ConnectionString, dbType);
        return Result(ok, msg);
    }

    [Tool(Name = "update_connection", Description = "修改数据库连接")]
    public CallToolResponse UpdateConnection([McpBody] UpdateConnectionRequest req)
    {
        var (ok, msg) = _conn.Update(req.Name, req.NewName, req.ConnectionString, req.DbType);
        return Result(ok, msg);
    }

    [Tool(Name = "delete_connection", Description = "删除数据库连接")]
    public CallToolResponse DeleteConnection([McpParam("要删除的连接名称")] string name)
    {
        var (ok, msg) = _conn.Delete(name);
        return Result(ok, msg);
    }

    [Tool(Name = "list_connections", Description = "列出所有已保存的数据库连接")]
    public CallToolResponse ListConnections()
    {
        var list = _conn.List();
        if (list.Count == 0)
            return Ok("暂无保存的数据库连接");

        return Ok(JsonSerializer.Serialize(list, JsonOpts));
    }

    // ── 辅助 ──
    private static CallToolResponse Result(bool ok, string msg) => new()
    {
        Content = [new() { Text = ok ? $"[OK] {msg}" : $"[ERR] {msg}", Type = "text" }],
        IsError = !ok,
    };

    private static CallToolResponse Ok(string text) => new()
    {
        Content = [new() { Text = text, Type = "text" }],
    };

    private static CallToolResponse Error(string msg) => new()
    {
        Content = [new() { Text = $"[ERR] {msg}", Type = "text" }],
        IsError = true,
    };
}
