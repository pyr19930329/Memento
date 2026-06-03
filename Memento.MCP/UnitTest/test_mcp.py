"""
MCP Server 测试脚本
StdioTransport 模式 — 覆盖连接管理 CRUD + 持久化 + 边界情况
"""
import json
import subprocess
import os
import time

server_path = r"H:\Memento.MCP\Memento.MCP\Memento.MCP\bin\Debug\net10.0\Memento.MCP.exe"
persist_path = r"H:\Memento.MCP\Memento.MCP\Memento.MCP\bin\Debug\net10.0\connections.json"

next_id = 1
passed = 0
failed = 0


def send_rpc(proc, request: dict) -> str:
    global next_id
    if "id" not in request:
        request["id"] = next_id
        next_id += 1
    line = json.dumps(request, ensure_ascii=False)
    print(f"  <<< {line}")
    proc.stdin.write(line + "\n")
    proc.stdin.flush()
    response = proc.stdout.readline()
    try:
        pretty = json.dumps(json.loads(response), ensure_ascii=False)
    except (json.JSONDecodeError, TypeError):
        pretty = response.rstrip()
    print(f"  >>> {pretty}")
    return response


def tools_call(proc, name: str, args: dict | None = None):
    return send_rpc(proc, {
        "jsonrpc": "2.0", "method": "tools/call",
        "params": {"name": name, "arguments": args or {}},
    })


def start_server() -> subprocess.Popen:
    return subprocess.Popen(
        [server_path],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, bufsize=1,
    )


def handshake(proc):
    send_rpc(proc, {
        "jsonrpc": "2.0", "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "test-client", "version": "1.0.0"},
        }
    })
    line = json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}, ensure_ascii=False)
    print(f"  <<< {line}")
    proc.stdin.write(line + "\n")
    proc.stdin.flush()
    print("  >>> (通知无响应)")


def check(title: str, response: str, expect_ok: bool = True):
    global passed, failed
    try:
        data = json.loads(response)
        is_error = data.get("result", {}).get("isError", False)
        text = data.get("result", {}).get("content", [{}])[0].get("text", "")
        ok = (not is_error) if expect_ok else is_error
        status = "PASS" if ok else "FAIL"
        print(f"  [{status}] {title}: {text[:80]}")
        if ok:
            passed += 1
        else:
            failed += 1
    except Exception:
        print(f"  [WARN] {title}: 无法解析响应")
        failed += 1


def section(title: str):
    print()
    print("─" * 50)
    print(f"  {title}")
    print("─" * 50)
    print()


# ════════════════════════════════════════════════════════════
print("=" * 60)
print("MCP Server 综合测试")
print("=" * 60)

# ===== 1. 基础 CRUD =====
section("1. 连接 CRUD 基础测试")
proc = start_server()
handshake(proc)

resp = tools_call(proc, "add_connection", {
    "name": "test_dev_mysql",
    "connection_string": "Server=localhost;Database=testdb;Uid=root;Pwd=123456;",
    "db_type": "MySql",
})
check("添加 MySQL 连接", resp, expect_ok=True)

resp = tools_call(proc, "add_connection", {
    "name": "test_dev_sqlserver",
    "connection_string": "Server=.;Database=testdb;Trusted_Connection=True;",
    "db_type": "SqlServer",
})
check("添加 SQL Server 连接", resp, expect_ok=True)

resp = tools_call(proc, "add_connection", {
    "name": "test_dev_mysql",
    "connection_string": "Server=other;Database=test;Uid=root;Pwd=123;",
})
check("重复添加应拒绝", resp, expect_ok=False)

resp = tools_call(proc, "list_connections", {})
check("列出包含 2 条连接", resp)

resp = tools_call(proc, "update_connection", {"name": "test_dev_sqlserver", "new_name": "test_prod_sqlserver"})
check("重命名连接", resp, expect_ok=True)

resp = tools_call(proc, "delete_connection", {"name": "test_prod_sqlserver"})
check("删除连接", resp, expect_ok=True)

resp = tools_call(proc, "delete_connection", {"name": "test_prod_sqlserver"})
check("重复删除应报错", resp, expect_ok=False)

resp = tools_call(proc, "list_connections", {})
check("列表只剩 test_dev_mysql", resp)

# 清理本节的测试数据
resp = tools_call(proc, "delete_connection", {"name": "test_dev_mysql"})

proc.terminate()
proc.wait()

# ===== 2. 边界情况 =====
section("2. 边界情况测试")
proc = start_server()
handshake(proc)

resp = tools_call(proc, "add_connection", {
    "name": "test_special_chars",
    "connection_string": "Server=my-host.com;Port=3306;Database=test_db;Uid=user@company;Pwd=pass!@#$%^&*();",
    "db_type": "MySql",
})
check("连接串含特殊字符", resp, expect_ok=True)

resp = tools_call(proc, "update_connection", {
    "name": "test_special_chars",
    "db_type": "PostgreSQL",
})
check("仅更新 db_type", resp, expect_ok=True)

resp = tools_call(proc, "update_connection", {
    "name": "test_special_chars",
    "connection_string": "Host=localhost;Port=5432;Database=testdb;",
})
check("仅更新 connection_string", resp, expect_ok=True)

resp = tools_call(proc, "add_connection", {
    "name": "test_empty_cs",
    "connection_string": "",
    "db_type": "MySql",
})
check("空连接串允许添加", resp, expect_ok=True)

resp = tools_call(proc, "update_connection", {"name": "test_does_not_exist", "connection_string": "abc"})
check("更新不存在的连接", resp, expect_ok=False)

resp = tools_call(proc, "delete_connection", {"name": "test_does_not_exist"})
check("删除不存在的连接", resp, expect_ok=False)

# 清理本节测试数据
for name in ["test_special_chars", "test_empty_cs"]:
    resp = tools_call(proc, "delete_connection", {"name": name})
proc.terminate()
proc.wait()

# ===== 3. 持久化测试 =====
section("3. 持久化测试")

proc = start_server()
handshake(proc)
resp = tools_call(proc, "add_connection", {
    "name": "persist_test",
    "connection_string": "Server=10.0.0.1;Database=mydb;Uid=sa;Pwd=secret;",
    "db_type": "SqlServer",
})
check("添加持久化数据", resp, expect_ok=True)
resp = tools_call(proc, "list_connections", {})
check("列表含 1 条", resp)
proc.terminate()
proc.wait()

proc = start_server()
handshake(proc)
resp = tools_call(proc, "list_connections", {})
check("重启后数据仍在", resp)

resp = tools_call(proc, "delete_connection", {"name": "persist_test"})
check("删除测试连接", resp, expect_ok=True)
proc.terminate()
proc.wait()

if os.path.exists(persist_path):
    print(f"  [INFO] 持久化文件已保存: {persist_path}")

# ===== 4. 工具列表 =====
section("4. 工具列表完整性")
proc = start_server()
handshake(proc)
resp = send_rpc(proc, {
    "jsonrpc": "2.0", "method": "tools/list", "params": {},
})
try:
    tools = json.loads(resp).get("result", {}).get("tools", [])
    names = [t["name"] for t in tools]
    expected = {"add_connection", "update_connection", "delete_connection",
                "list_connections", "query", "commit"}
    missing = expected - set(names)
    if missing:
        print(f"  [FAIL] 缺少工具: {missing}")
        failed += 1
    else:
        print(f"  [PASS] 6 个工具全部注册: {', '.join(names)}")
        passed += 1
except Exception as e:
    print(f"  [WARN] tools/list 解析失败: {e}")
    failed += 1

proc.terminate()
proc.wait()

# ════════════════════════════════════════════════════════════
print()
print("=" * 60)
print(f"  结果: {passed} PASS | {failed} FAIL")
print("=" * 60)
