"""
MCP Server 交互式测试工具
选择序号 → 看 arguments 模板 → 粘贴 JSON → 自动发送
"""
import json
import subprocess

SERVER = r"H:\Memento.MCP\Memento.MCP\Memento.MCP\bin\Debug\net10.0\Memento.MCP.exe"


def _example_value(pname: str, pschema: dict) -> str:
    ptype = pschema.get("type", "string")
    examples = {
        "string": f"example_{pname}",
        "number": "123", "integer": "123", "boolean": "true",
        "array": '["item1", "item2"]', "object": '{"key": "value"}',
    }
    return examples.get(ptype, "example_" + pname)


def _display_response(resp_raw: str):
    """格式化显示响应：JSON 数组转表格，其余原样展示"""
    try:
        data = json.loads(resp_raw)
        content = data.get("result", {}).get("content", [])
        if not content:
            # 重新序列化避免 \uXXXX 转义
            print(f"  返回: {json.dumps(data, ensure_ascii=False)}")
            return
        text = content[0].get("text", "")
        # 尝试把 text 字段也当做 JSON 解析
        try:
            inner = json.loads(text)
            if isinstance(inner, list) and len(inner) > 0 and isinstance(inner[0], dict):
                # 格式化为表格
                keys = list(inner[0].keys())
                col_widths = {k: len(k) for k in keys}
                for row in inner:
                    for k in keys:
                        val = str(row.get(k, ""))
                        col_widths[k] = max(col_widths[k], len(val))
                header = "  " + "  ".join(k.ljust(col_widths[k]) for k in keys)
                sep = "  " + "  ".join("\u2500" * col_widths[k] for k in keys)
                print(f"  {sep}")
                print(f"  {header}")
                print(f"  {sep}")
                for row in inner:
                    line = "  " + "  ".join(str(row.get(k, "")).ljust(col_widths[k]) for k in keys)
                    print(f"  {line}")
                print(f"  {sep}")
                print(f"  共 {len(inner)} 条记录")
                return
        except (json.JSONDecodeError, TypeError, IndexError):
            pass
        # 非表格：重新序列化避免 \uXXXX 转义
        print(f"  返回: {json.dumps(data, ensure_ascii=False)}")
    except json.JSONDecodeError:
        print(f"  返回: {resp_raw}")


def main():
    proc = subprocess.Popen(
        [SERVER],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, bufsize=1,
    )
    next_id = 1

    def send(req: dict):
        nonlocal next_id
        if "id" not in req:
            req["id"] = next_id
            next_id += 1
        line = json.dumps(req, ensure_ascii=False)
        proc.stdin.write(line + "\n")
        proc.stdin.flush()
        resp = proc.stdout.readline()
        return resp  # 返回原始响应，由 _display_response 处理

    # ── 握手 ──
    print("=" * 54)
    print("  Memento.MCP — 交互式测试终端")
    print("=" * 54)
    send({"jsonrpc": "2.0", "method": "initialize",
          "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                     "clientInfo": {"name": "manual", "version": "1.0.0"}}})
    proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
    proc.stdin.flush()
    print("  已连接\n")

    # ── 获取工具列表 ──
    resp = send({"jsonrpc": "2.0", "method": "tools/list", "params": {}})
    tools = json.loads(resp).get("result", {}).get("tools", [])

    # ── 主循环 ──
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
        required = tool.get("inputSchema", {}).get("required", [])

        print(f"\n  ── {name} ──")
        print(f"  {tool.get('description', '')}")
        print()

        # 无参数工具直接执行
        if not props:
            req = {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {"name": name, "arguments": {}},
            }
            print(f"  发送: {json.dumps(req, ensure_ascii=False)}")
            resp = send(req)
            _display_response(resp)
            print()
            continue

        # 有参数工具：显示模板 + 输入
        example_args = {}
        for pname, pschema in props.items():
            example_args[pname] = _example_value(pname, pschema)

        print(f"  参数模板：")
        for line in json.dumps(example_args, ensure_ascii=False, indent=2).split("\n"):
            print(f"    {line}")
        print()
        print(f"  输入 arguments JSON（直接回车跳过）：")

        args_input = input("  > ").strip()
        if not args_input:
            print()
            continue

        try:
            args = json.loads(args_input)
        except json.JSONDecodeError as e:
            print(f"  参数解析失败: {e}")
            print()
            continue

        if not isinstance(args, dict):
            print("  参数必须是 JSON 对象")
            print()
            continue

        print()
        req = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {"name": name, "arguments": args},
        }
        print(f"  发送: {json.dumps(req, ensure_ascii=False)}")
        resp = send(req)
        _display_response(resp)
        print()

    proc.terminate()
    proc.wait()
    print("再见")


if __name__ == "__main__":
    main()
