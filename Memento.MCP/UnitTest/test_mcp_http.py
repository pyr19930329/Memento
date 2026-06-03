"""
MCP Server HTTP 测试脚本 — 通过 HTTP POST 调用 MCP 工具
"""
import json
import os
import urllib.request

BASE_URL = os.environ.get("MCP_URL", "http://localhost:9876/mcp")

passed = 0
failed = 0


def rpc(method: str, params: dict | None = None, id: int = 1) -> dict:
    """发送 JSON-RPC 请求并返回响应"""
    req = {"jsonrpc": "2.0", "id": id, "method": method}
    if params is not None:
        req["params"] = params
    data = json.dumps(req, ensure_ascii=False).encode("utf-8")
    http = urllib.request.Request(BASE_URL, data=data,
        headers={"Content-Type": "application/json"},
        method="POST")
    with urllib.request.urlopen(http) as resp:
        return json.loads(resp.read().decode("utf-8"))


def check(title: str, resp: dict, expect_ok: bool = True):
    global passed, failed
    try:
        is_error = resp.get("result", {}).get("isError", False)
        text = resp.get("result", {}).get("content", [{}])[0].get("text", "")
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
print("MCP Server HTTP 测试")
print(f"  Target: {BASE_URL}")
print("=" * 60)

# ===== 1. 初始化 =====
section("0. 初始化")
rpc("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test", "version": "1.0.0"}})
rpc("notifications/initialized")
print("  [OK] 已连接")

# ===== 2. 连接 CRUD =====
section("1. 连接 CRUD 基础测试")

resp = rpc("tools/call", {"name": "add_connection", "arguments": {
    "name": "test_dev_mysql", "host": "localhost", "port": 3306,
    "user_id": "root", "password": "123456", "db_type": "mysql",
}}, id=2)
check("添加 MySQL 连接", resp, expect_ok=False)

resp = rpc("tools/call", {"name": "add_connection", "arguments": {
    "name": "test_dev_mysql", "host": "other", "port": 3306,
    "user_id": "root", "password": "123",
}}, id=3)
check("重复添加应拒绝", resp, expect_ok=False)

resp = rpc("tools/call", {"name": "list_connections", "arguments": {}}, id=4)
check("列出所有连接", resp)

resp = rpc("tools/call", {"name": "delete_connection", "arguments": {"name": "test_dev_mysql"}}, id=5)
check("删除不存在的连接", resp, expect_ok=False)

resp = rpc("tools/call", {"name": "update_connection", "arguments": {"name": "test_does_not_exist"}}, id=6)
check("更新不存在的连接", resp, expect_ok=False)

resp = rpc("tools/call", {"name": "delete_connection", "arguments": {"name": "test_does_not_exist"}}, id=7)
check("删除不存在的连接", resp, expect_ok=False)

# ===== 3. 工具列表 =====
section("2. 工具列表完整性")

resp = rpc("tools/list", {}, id=100)
try:
    tools = resp.get("result", {}).get("tools", [])
    names = [t["name"] for t in tools]
    expected = {"add_connection", "update_connection", "delete_connection",
                "list_connections", "query", "execute",
                "list_databases", "create_database", "alter_database", "drop_database",
                "describe_table", "create_table", "alter_table", "drop_table", "truncate_table",
                "list_tables", "audit_by_table", "audit_recent"}
    missing = expected - set(names)
    if missing:
        print(f"  [FAIL] 缺少工具: {missing}")
        failed += 1
    else:
        print(f"  [PASS] {len(tools)} 个工具全部注册: {', '.join(names)}")
        passed += 1
except Exception as e:
    print(f"  [WARN] tools/list 解析失败: {e}")
    failed += 1

print()
print("=" * 60)
print(f"  结果: {passed} PASS | {failed} FAIL")
print("=" * 60)
