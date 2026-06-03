# MCP Server 快速测试脚本
# 通过 stdin/stdout 发送 JSON-RPC 消息测试 StdioTransport 模式的服务

$ErrorActionPreference = "Stop"

$serverPath = "H:\Memento.MCP\Memento.MCP\Memento.MCP\bin\Debug\net10.0\Memento.MCP.exe"

# 启动 MCP 服务器进程（stdin/stdout 模式）
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $serverPath
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$process.Start() | Out-Null

# 辅助函数：发送 JSON-RPC 请求并读取响应
function Send-RpcMessage {
    param([string]$jsonMessage)

    $process.StandardInput.WriteLine($jsonMessage)
    $process.StandardInput.Flush()

    # 读取响应（以 \n 结尾）
    $response = $process.StandardOutput.ReadLine()
    return $response
}

# 1. 初始化
Write-Host "=== 1. 初始化 ===" -ForegroundColor Cyan
$init = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{ name = "test-client"; version = "1.0.0" }
    }
} | ConvertTo-Json -Depth 5
Write-Host "<<< $init" -ForegroundColor Green
$resp = Send-RpcMessage -jsonMessage $init
Write-Host ">>> $resp" -ForegroundColor Yellow
Write-Host ""

# 2. 发送 initialized 通知（无响应）
Write-Host "=== 2. initialized 通知 ===" -ForegroundColor Cyan
$notify = @{
    jsonrpc = "2.0"
    method = "notifications/initialized"
} | ConvertTo-Json
Write-Host "<<< $notify" -ForegroundColor Green
$process.StandardInput.WriteLine($notify)
$process.StandardInput.Flush()
Write-Host ">>> (无响应，通知类消息)" -ForegroundColor Yellow
Write-Host ""

# 3. 列出工具
Write-Host "=== 3. 列出工具 (tools/list) ===" -ForegroundColor Cyan
$list = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
    params = @{}
} | ConvertTo-Json
Write-Host "<<< $list" -ForegroundColor Green
$resp = Send-RpcMessage -jsonMessage $list
Write-Host ">>> $resp" -ForegroundColor Yellow
Write-Host ""

# 4. 调用 query 工具
Write-Host "=== 4. 调用工具 (tools/call: query) ===" -ForegroundColor Cyan
$call = @{
    jsonrpc = "2.0"
    id = 3
    method = "tools/call"
    params = @{
        name = "query"
        arguments = @{ sql = "SELECT * FROM users" }
    }
} | ConvertTo-Json -Depth 5
Write-Host "<<< $call" -ForegroundColor Green
$resp = Send-RpcMessage -jsonMessage $call
Write-Host ">>> $resp" -ForegroundColor Yellow
Write-Host ""

# 清理
$process.Kill()
$process.Dispose()

Write-Host "=== 测试完成 ===" -ForegroundColor Magenta
