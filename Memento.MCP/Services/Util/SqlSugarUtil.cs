using SqlSugar;

namespace Memento.MCP.Services.Util;

public class SqlSugarUtil
{
    public static SqlSugarClient GetClient(string connectionString, string dbType) {
        return new SqlSugarClient(new ConnectionConfig {
            ConnectionString = connectionString,
            DbType = dbType.Trim() switch {
                "MySql" or "mysql" or "MYSQL" => DbType.MySql,
                "SqlServer" or "sqlserver" or "SQLSERVER" => DbType.SqlServer,
                "PostgreSQL" or "postgresql" or "POSTGRESQL" or "Postgres" or "postgres" => DbType.PostgreSQL,
                "Oracle" or "oracle" or "ORACLE" => DbType.Oracle,
                _ => throw new ArgumentException($"不支持的数据库类型 '{dbType}'，支持的：MySql / SqlServer / PostgreSQL / Oracle"),
            },
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }
    
    public static SqlSugarClient GetClientByName(string connectionName) {
        ConnectionManager manager = new();
        var dbConnectionInfo = manager.Get(connectionName);
        if (dbConnectionInfo is null) throw new ArgumentException($"连接 '{connectionName}' 不存在");
        if (string.IsNullOrEmpty(dbConnectionInfo.ConnectionString)) throw new ArgumentException($"连接 '{connectionName}' 的连接字符串为空");
        if (string.IsNullOrEmpty(dbConnectionInfo.DbType)) throw new ArgumentException($"连接 '{connectionName}' 的数据库类型为空");
        
        return GetClient(dbConnectionInfo.ConnectionString, dbConnectionInfo.DbType);
    }

    /// <summary>测试数据库连接是否可达，失败时抛出异常</summary>
    public static void TestConnection(string connectionString, string dbType)
    {
        var client = GetClient(connectionString, dbType);
        if (!client.Ado.IsValidConnection())
            throw new InvalidOperationException("数据库连接失败");
    }
}