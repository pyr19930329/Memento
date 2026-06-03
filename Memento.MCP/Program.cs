using McpDotNet.Protocol.Transport;
using McpDotNet.Server;
using Memento.MCP.Register;
using Memento.MCP.Services;

// ── 1. 服务 ──
ConnectionManager connManager = new();

// ── 2. 自动注册 Toolsets ──
McpToolRegistry registry = new();
registry.RegisterAllToolsets(connManager);

// ── 3. 启动服务器 ──
McpServerOptions options = new()
{
    ServerInfo = new() { Name = "Memento.MCP", Version = "1.0.0" },
    Capabilities = registry.BuildCapabilities(),
};

await using IMcpServer server = McpServerFactory.Create(new StdioServerTransport("Memento.MCP"), options);
await server.StartAsync();

await Task.Delay(Timeout.Infinite);
