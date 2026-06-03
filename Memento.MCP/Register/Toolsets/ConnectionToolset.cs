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

    [Tool(Name = "add_connection", Description = "添加数据库连接")]
    public CallToolResponse AddConnection([McpBody] AddConnectionRequest req) {
        try {
            if (string.IsNullOrWhiteSpace(req.Name)) return Error("连接名称不能为空");
            if (string.IsNullOrWhiteSpace(req.Host)) return Error("主机地址不能为空");
            if (req.Port <= 0) return Error("端口无效");
            if (string.IsNullOrWhiteSpace(req.UserId)) return Error("用户名不能为空");
            if (_conn.Get(req.Name) != null) return Error($"连接 '{req.Name}' 已存在");

            var cs = BuildConnectionString(req.Host, req.Port, "", req.UserId, req.Password, req.DbType ?? "MySql");
            if (cs.StartsWith("[ERR]")) return Error(cs[5..]);

            var client = SqlSugarUtil.GetClient(cs, req.DbType ?? "MySql");
            var result = client.Ado.IsValidConnection();
            if (!result) return Error("数据库连接失败");

            var dbType = req.DbType ?? "MySql";
            var (ok, msg) = _conn.Add(req.Name, cs, dbType);
            return Result(ok, msg);
        }
        catch (Exception ex) {
            return Error($"添加连接失败: {ex.Message}");
        }
    }

    [Tool(Name = "update_connection", Description = "修改数据库连接")]
    public CallToolResponse UpdateConnection([McpBody] UpdateConnectionRequest req) {
        try {
            if (string.IsNullOrWhiteSpace(req.Name)) return Error("连接名称不能为空");

            var existing = _conn.Get(req.Name);
            if (existing == null) return Error($"连接 '{req.Name}' 不存在");

            var dbType = req.DbType ?? existing.DbType;
            var host = req.Host ?? ExtractHost(existing.ConnectionString);
            var port = req.Port ?? ExtractPort(existing.ConnectionString, dbType);
            var database = ExtractDatabase(existing.ConnectionString);
            var userId = req.UserId ?? ExtractUserId(existing.ConnectionString);
            var password = req.Password ?? ExtractPassword(existing.ConnectionString);

            var cs = BuildConnectionString(host, port, database, userId, password, dbType);
            if (cs.StartsWith("[ERR]")) return Error(cs[5..]);

            var client = SqlSugarUtil.GetClient(cs, dbType);
            var result = client.Ado.IsValidConnection();
            if (!result) return Error("数据库连接失败");

            var (ok, msg) = _conn.Update(req.Name, req.NewName, cs, dbType);
            return Result(ok, msg);
        }
        catch (Exception ex) {
            return Error($"更新连接失败: {ex.Message}");
        }
    }

    [Tool(Name = "delete_connection", Description = "删除数据库连接")]
    public CallToolResponse DeleteConnection([McpParam("要删除的连接名称")] string name) {
        if (string.IsNullOrWhiteSpace(name)) return Error("连接名称不能为空");
        var (ok, msg) = _conn.Delete(name);
        return Result(ok, msg);
    }

    [Tool(Name = "list_connections", Description = "列出所有已保存的数据库连接")]
    public CallToolResponse ListConnections() {
        var list = _conn.List();
        if (list.Count == 0) return Ok("暂无保存的数据库连接");
        return Ok(JsonSerializer.Serialize(list, JsonOpts));
    }

    // ── 连接字符串生成 ──

    private static string BuildConnectionString(string host, int port, string database, string userId, string password, string dbType) {
        return dbType.ToLowerInvariant() switch {
            "mysql" => $"Server={host};Port={port};Database={database};Uid={userId};Pwd={password};Charset=utf8;Allow User Variables=True;Connect Timeout=10;AllowLoadLocalInfile=true;",
            "sqlserver" => $"Data Source={host},{port};Initial Catalog={database};User ID={userId};Password={password};TrustServerCertificate=True;Connect Timeout=10;",
            "postgresql" or "postgres" => $"Host={host};Port={port};Database={database};User ID={userId};Password={password};Pooling=true;Timeout=10;",
            _ => "[ERR]不支持的数据库类型",
        };
    }

    // ── 从已有连接串中提取字段（用于更新时保留未传的参数）──

    private static string ExtractHost(string cs) {
        var parts = cs.Split(';').Select(p => p.Split('=', 2)).Where(a => a.Length == 2);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in parts) dict[a[0].Trim()] = a[1].Trim();
        return dict.GetValueOrDefault("Server") ?? dict.GetValueOrDefault("Host") ?? dict.GetValueOrDefault("Data Source") ?? "";
    }

    private static int ExtractPort(string cs, string dbType) {
        var parts = cs.Split(';').Select(p => p.Split('=', 2)).Where(a => a.Length == 2);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in parts) dict[a[0].Trim()] = a[1].Trim();
        if (dict.TryGetValue("Port", out var p) && int.TryParse(p, out var port)) return port;
        return dbType.ToLowerInvariant() switch { "mysql" => 3306, "sqlserver" => 1433, _ => 5432 };
    }

    private static string ExtractDatabase(string cs) {
        var parts = cs.Split(';').Select(p => p.Split('=', 2)).Where(a => a.Length == 2);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in parts) dict[a[0].Trim()] = a[1].Trim();
        return dict.GetValueOrDefault("Database") ?? dict.GetValueOrDefault("Initial Catalog") ?? "";
    }

    private static string ExtractUserId(string cs) {
        var parts = cs.Split(';').Select(p => p.Split('=', 2)).Where(a => a.Length == 2);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in parts) dict[a[0].Trim()] = a[1].Trim();
        return dict.GetValueOrDefault("Uid") ?? dict.GetValueOrDefault("User ID") ?? dict.GetValueOrDefault("User Id") ?? "";
    }

    private static string ExtractPassword(string cs) {
        var parts = cs.Split(';').Select(p => p.Split('=', 2)).Where(a => a.Length == 2);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in parts) dict[a[0].Trim()] = a[1].Trim();
        return dict.GetValueOrDefault("Pwd") ?? dict.GetValueOrDefault("Password") ?? "";
    }

    // ── 辅助 ──
    private static CallToolResponse Result(bool ok, string msg) => new() {
        Content = [new() { Text = ok ? $"[OK] {msg}" : $"[ERR] {msg}", Type = "text" }],
        IsError = !ok,
    };

    private static CallToolResponse Ok(string text) => new() {
        Content = [new() { Text = text, Type = "text" }],
    };

    private static CallToolResponse Error(string msg) => new() {
        Content = [new() { Text = $"[ERR] {msg}", Type = "text" }],
        IsError = true,
    };
}
