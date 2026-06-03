using Memento.MCP.Register;
using Memento.MCP.Services;

// ── 1. 构建 ASP.NET Core 应用 ──
var builder = WebApplication.CreateBuilder(args);

// ── 2. 端口配置：命令行参数 > appsettings.json > 默认 8080 ──
var port = args.Length > 0 ? args[0] : builder.Configuration["Port"] ?? "9876";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// ── 3. 全局异常 ──
GlobalException.Register();

// ── 4. 服务 + 自动注册 Toolsets ──
ConnectionManager connManager = new();
McpToolRegistry registry = new();
registry.RegisterAllToolsets(connManager);

// ── 5. 注册 MCP SSE 端点 ──
McpSseEndpoint.Map(app, registry);

// ── 6. 启动 ──
Console.Error.WriteLine($"[MCP] SSE server starting on http://localhost:{port}/mcp");
app.Run();
