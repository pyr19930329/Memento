using McpDotNet.Protocol.Types;
using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register.Toolsets;

[McpToolset]
public class DataToolset
{
    [Tool(Name = "query", Description = "执行 SQL 查询")]
    public CallToolResponse Query([McpParam("SQL 查询语句")] string sql)
    {
        // TODO: 接入 SqlSugar db.Ado.SqlQueryDynamicAsync(sql)
        return new() { Content = [new() { Text = $"查询: {sql}", Type = "text" }] };
    }

    [Tool(Name = "commit", Description = "记录当前数据快照")]
    public CallToolResponse Commit([McpParam("表名")] string table)
    {
        // TODO: 接入 SqlSugar 快照逻辑
        return new() { Content = [new() { Text = $"快照已记录: {table}", Type = "text" }] };
    }
}
