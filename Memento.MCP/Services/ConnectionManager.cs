using System.Text.Encodings.Web;
using System.Text.Json;

namespace Memento.MCP.Services;

/// <summary>数据库连接信息（仅存储，实际连接由 SqlSugar 实现）</summary>
public class DbConnectionInfo
{
    public string Name { get; set; } = "";
    public string ConnectionString { get; set; } = "";

    /// <summary>数据库类型：MySql / SqlServer / PostgreSQL 等</summary>
    public string DbType { get; set; } = "MySql";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>数据库连接管理器 — 管理连接字符串，自动持久化到本地 JSON 文件</summary>
public class ConnectionManager
{
    private readonly string _filePath;
    private readonly Dictionary<string, DbConnectionInfo> _connections = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="filePath">持久化文件路径，默认在 exe 目录下 connections.json</param>
    public ConnectionManager(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "connections.json");
        Load();
    }

    // ── 持久化 ──

    private void Load()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<DbConnectionInfo>>(json);
            if (list == null) return;

            foreach (var item in list)
            {
                _connections[item.Name] = item;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionManager] 加载失败: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_connections.Values.ToList(), JsonOpts);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionManager] 保存失败: {ex.Message}");
        }
    }

    // ── CRUD ──

    public (bool Success, string Message) Add(string name, string connectionString, string dbType = "MySql")
    {
        if (_connections.ContainsKey(name))
            return (false, $"连接 '{name}' 已存在");

        _connections[name] = new DbConnectionInfo {
            Name = name,
            ConnectionString = connectionString,
            DbType = dbType,
        };

        Save();
        return (true, $"连接 '{name}' 已添加 (DbType={dbType})");
    }

    public (bool Success, string Message) Update(
        string name,
        string? newName = null,
        string? connectionString = null,
        string? dbType = null)
    {
        if (!_connections.TryGetValue(name, out var info)) return (false, $"连接 '{name}' 不存在");
        if (connectionString != null) info.ConnectionString = connectionString;
        if (dbType != null) info.DbType = dbType;
        info.UpdatedAt = DateTime.UtcNow;

        if (newName != null && newName != name) {
            if (_connections.ContainsKey(newName))
                return (false, $"新名称 '{newName}' 已存在");
            _connections.Remove(name);
            info.Name = newName;
            _connections[newName] = info;
        }

        Save();
        return (true, $"连接 '{newName ?? name}' 已更新");
    }

    public (bool Success, string Message) Delete(string name)
    {
        if (!_connections.Remove(name))
            return (false, $"连接 '{name}' 不存在");

        Save();
        return (true, $"连接 '{name}' 已删除");
    }

    public List<DbConnectionInfo> List() =>
        _connections.Values.OrderBy(c => c.Name).ToList();

    public DbConnectionInfo? Get(string name)
    {
        _connections.TryGetValue(name, out var info);
        return info;
    }
}
