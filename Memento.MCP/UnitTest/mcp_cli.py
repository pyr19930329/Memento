"""
MCP Server 交互式测试工具 — HTTP 模式
"""
import json
import os
import urllib.request

BASE_URL = os.environ.get("MCP_URL", "http://localhost:18080/mcp")
next_id = 100


def rpc(method: str, params: dict | None = None) -> dict:
    global next_id
    req = {"jsonrpc": "2.0", "id": next_id, "method": method}
    next_id += 1
    if params is not None:
        req["params"] = params
    data = json.dumps(req, ensure_ascii=False).encode("utf-8")
    http = urllib.request.Request(BASE_URL, data=data,
        headers={"Content-Type": "application/json"},
        method="POST")
    with urllib.request.urlopen(http) as resp:
        return json.loads(resp.read().decode("utf-8"))


def display(resp: dict):
    content = resp.get("result", {}).get("content", [])
    if not content:
        print(f"  返回: {json.dumps(resp, ensure_ascii=False)}")
        return
    text = content[0].get("text", "")
    try:
        inner = json.loads(text)
        if isinstance(inner, list) and len(inner) > 0 and isinstance(inner[0], dict):
            keys = list(inner[0].keys())
            col_widths = {k: len(k) for k in keys}
            for row in inner:
                for k in keys:
                    val = str(row.get(k, ""))
                    col_widths[k] = max(col_widths[k], len(val))
            sep = "  " + "  ".join("\u2500" * col_widths[k] for k in keys)
            print(f"  {sep}")
            print("  " + "  ".join(k.ljust(col_widths[k]) for k in keys))
            print(f"  {sep}")
            for row in inner:
                line = "  " + "  ".join(str(row.get(k, "")).ljust(col_widths[k]) for k in keys)
                print(line)
            print(f"  {sep}")
            print(f"  共 {len(inner)} 条记录")
            return
    except (json.JSONDecodeError, TypeError, IndexError):
        pass
    print(f"  返回: {json.dumps(resp, ensure_ascii=False)}")


def main():
    print("=" * 54)
    print("  Memento.MCP — HTTP 交互式终端")
    print(f"  Target: {BASE_URL}")
    print("=" * 54)

    rpc("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "cli", "version": "1.0.0"}})
    rpc("notifications/initialized")
    print("  已连接\n")

    resp = rpc("tools/list")
    tools = resp.get("result", {}).get("tools", [])

    while True:
        print("─" * 54)
        print("  工具列表：")
        for i, t in enumerate(tools, 1):
            print(f"    {i:2d}. {t['name']}  — {t.get('description', '')}")
        print(f"    q. 退出")
        print("─" * 54)

        raw = input("选择序号 > ").strip()
        if raw.lower() in ("q", "quit", "exit", ""):
            break

        try:
            idx = int(raw) - 1
            if idx < 0 or idx >= len(tools):
                print(f"  请输入 1-{len(tools)} 之间的数字\n")
                continue
        except ValueError:
            print("  请输入数字\n")
            continue

        tool = tools[idx]
        name = tool["name"]
        props = tool.get("inputSchema", {}).get("properties", {})

        print(f"\n  ── {name} ──")
        print(f"  {tool.get('description', '')}\n")

        if not props:
            resp = rpc("tools/call", {"name": name, "arguments": {}})
            display(resp)
            print()
            continue

        example_args = {}
        for pname, pschema in props.items():
            ptype = pschema.get("type", "string")
            examples = {
                "string": f"example_{pname}",
                "number": "123", "integer": "123", "boolean": "true",
                "array": '["item1", "item2"]', "object": '{"key": "value"}',
            }
            example_args[pname] = examples.get(ptype, f"example_{pname}")

        print("  参数模板：")
        for line in json.dumps(example_args, ensure_ascii=False, indent=2).split("\n"):
            print(f"    {line}")
        print()
        print("  输入 arguments JSON（直接回车跳过）：")

        args_input = input("  > ").strip()
        if not args_input:
            print()
            continue

        try:
            args = json.loads(args_input)
        except json.JSONDecodeError as e:
            print(f"  参数解析失败: {e}\n")
            continue

        if not isinstance(args, dict):
            print("  参数必须是 JSON 对象\n")
            continue

        print()
        resp = rpc("tools/call", {"name": name, "arguments": args})
        display(resp)
        print()

    print("再见")


if __name__ == "__main__":
    main()
