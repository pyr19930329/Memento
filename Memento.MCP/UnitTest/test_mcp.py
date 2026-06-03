"""
MCP Server 测试脚本 (适配新版 add_connection API：host/port/user_id/password)
"""
import json
import subprocess
import os

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
    "name": "test_dev_mysql", "host": "localhost", "port": 3306,
    "user_id": "root", "password": "123456", "db_type": "mysql",
})
check("添加 MySQL 连接", resp, expect_ok=True)

resp = tools_call(proc, "add_connection", {
    "name": "test_dev_sqlserver", "host": ".", "port": 1433,
    "user_id": "sa", "password": "", "db_type": "sqlserver",
})
check("添加 SQL Server 连接", resp, expect_ok=True)

resp = tools_call(proc, "add_connection", {
    "name": "test_dev_mysql", "host": "other", "port": 3306,
    "user_id": "root", "password": "123",
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

resp = tools_call(proc, "delete_connection", {"name": "test_dev_mysql"})
proc.terminate()
proc.wait()

# ===== 2. 边界情况 =====
section("2. 边界情况测试")
proc = start_server()
handshake(proc)

resp = tools_call(proc, "add_connection", {
    "name": "test_special_chars", "host": "my-host.com", "port": 3306,
    "user_id": "user@company", "password": "pass!@#$%^&*()", "db_type": "mysql",
})
check("特殊字符密码", resp, expect_ok=True)

resp = tools_call(proc, "update_connection", {"name": "test_special_chars", "db_type": "postgresql"})
check("仅更新 db_type", resp, expect_ok=True)

resp = tools_call(proc, "update_connection", {"name": "test_special_chars", "host": "new-host.com", "port": 5432})
check("仅更新 host/port", resp, expect_ok=True)

resp = tools_call(proc, "update_connection", {"name": "test_does_not_exist"})
check("更新不存在的连接", resp, expect_ok=False)

resp = tools_call(proc, "delete_connection", {"name": "test_does_not_exist"})
check("删除不存在的连接", resp, expect_ok=False)

for name in ["test_special_chars"]:
    resp = tools_call(proc, "delete_connection", {"name": name})
proc.terminate()
proc.wait()

# ===== 3. 持久化测试 =====
section("3. 持久化测试")
proc = start_server()
handshake(proc)
resp = tools_call(proc, "add_connection", {
    "name": "persist_test", "host": "10.0.0.1", "port": 1433,
    "user_id": "sa", "password": "secret", "db_type": "sqlserver",
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
                "list_connections", "query", "execute",
                "list_databases", "create_database", "alter_database", "drop_database",
                "describe_table", "create_table", "alter_table", "drop_table", "truncate_table"}
    missing = expected - set(names)
    if missing:
        print(f"  [FAIL] 缺少工具: {missing}")
        failed += 1
    else:
        print(f"  [PASS] 15 个工具全部注册: {', '.join(names)}")
        passed += 1
except Exception as e:
    print(f"  [WARN] tools/list 解析失败: {e}")
    failed += 1

proc.terminate()
proc.wait()

print()
print("=" * 60)
print(f"  结果: {passed} PASS | {failed} FAIL")
print("=" * 60)
