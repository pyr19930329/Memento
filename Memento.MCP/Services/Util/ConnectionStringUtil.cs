namespace Memento.MCP.Services.Util;

/// <summary>连接字符串构建与字段提取工具</summary>
public static class ConnectionStringUtil
{
    /// <summary>根据各字段生成数据库连接字符串</summary>
    public static string Build(string host, int port, string database, string userId, string password, string dbType)
    {
        return dbType.ToLowerInvariant() switch
        {
            "mysql" => $"Server={host};Port={port};Database={database};Uid={userId};Pwd={password};Charset=utf8;Allow User Variables=True;Connect Timeout=10;AllowLoadLocalInfile=true;",
            "sqlserver" => $"Data Source={host},{port};Initial Catalog={database};User ID={userId};Password={password};TrustServerCertificate=True;Connect Timeout=10;",
            "postgresql" or "postgres" => $"Host={host};Port={port};Database={database};User ID={userId};Password={password};Pooling=true;Timeout=10;",
            _ => throw new ArgumentException($"不支持的数据库类型: {dbType}"),
        };
    }

    /// <summary>从连接字符串中提取主机地址</summary>
    public static string ExtractHost(string cs)
    {
        var dict = ParseConnectionString(cs);
        return dict.GetValueOrDefault("Server") ?? dict.GetValueOrDefault("Host") ?? dict.GetValueOrDefault("Data Source") ?? "";
    }

    /// <summary>从连接字符串中提取端口，若不存在则返回该类型的默认端口</summary>
    public static int ExtractPort(string cs, string dbType)
    {
        var dict = ParseConnectionString(cs);
        if (dict.TryGetValue("Port", out var p) && int.TryParse(p, out var port))
            return port;
        return dbType.ToLowerInvariant() switch { "mysql" => 3306, "sqlserver" => 1433, _ => 5432 };
    }

    /// <summary>从连接字符串中提取数据库名</summary>
    public static string ExtractDatabase(string cs)
    {
        var dict = ParseConnectionString(cs);
        return dict.GetValueOrDefault("Database") ?? dict.GetValueOrDefault("Initial Catalog") ?? "";
    }

    /// <summary>从连接字符串中提取用户名</summary>
    public static string ExtractUserId(string cs)
    {
        var dict = ParseConnectionString(cs);
        return dict.GetValueOrDefault("Uid") ?? dict.GetValueOrDefault("User ID") ?? dict.GetValueOrDefault("User Id") ?? "";
    }

    /// <summary>从连接字符串中提取密码</summary>
    public static string ExtractPassword(string cs)
    {
        var dict = ParseConnectionString(cs);
        return dict.GetValueOrDefault("Pwd") ?? dict.GetValueOrDefault("Password") ?? "";
    }

    // ── 内部辅助 ──

    private static Dictionary<string, string> ParseConnectionString(string cs)
    {
        return cs.Split(';')
            .Select(p => p.Split('=', 2))
            .Where(a => a.Length == 2)
            .ToDictionary(a => a[0].Trim(), a => a[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
