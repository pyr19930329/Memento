using Memento.MCP.Register.Toolsets;
using Memento.MCP.Services;

// ── 1. 构建 ASP.NET Core 应用 ──
var builder = WebApplication.CreateBuilder(args);

// ── 2. 端口配置：命令行参数 > appsettings.json > 默认 9876 ──
var port = args.Length > 0
    ? args[0]
    : (builder.Configuration["Port"] ?? "9876");
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── 3. 全局异常 ──
GlobalException.Register();

// ── 4. 注册服务 ──
builder.Services.AddSingleton<ConnectionManager>();

// ── 5. 注册 MCP 服务器 ──
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = false)
    .WithTools<ConnectionToolset>()
    .WithTools<DatabaseToolset>()
    .WithTools<TableToolset>()
    .WithTools<AuditToolset>();

var app = builder.Build();

// ── 6. 注册 MCP 端点 ──
app.MapMcp("/mcp");

// ── 7. 启动 ──
Console.Error.WriteLine($"[MCP] SSE server starting on http://localhost:{port}/sse");
app.Run();
