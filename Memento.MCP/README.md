# Memento.MCP

基于 ASP.NET Core 的 MCP (Model Context Protocol) 服务器，提供数据库连接管理、数据库/表 CRUD 操作、SQL 查询执行及审计日志功能。

## 技术栈

- **.NET 10** + **ASP.NET Core** — Web 服务器 & SSE 传输
- **ModelContextProtocol** — 官方 MCP C# SDK
- **SqlSugarCore** — ORM (MySQL / SqlServer / PostgreSQL / Oracle)
- **Streamable HTTP** — SSE 端点 + POST JSON-RPC

## 快速开始

### 配置

编辑 `appsettings.json`：

```json
{
  "Port": 9876
}
```

端口优先级：命令行参数 > appsettings.json > 默认 9876。

### 启动

```bash
# 默认端口 9876
Memento.MCP.exe

# 指定端口
Memento.MCP.exe 8080
```

### 连接

服务端暴露两个端点：

| 方法 | 端点 | 说明 |
|------|------|------|
| `GET` | `/sse`, `/mcp` | SSE 流（Rider/客户端连接） |
| `POST` | `/mcp` | JSON-RPC 请求 |

**Rider 配置（项目级 `.idea/mcp.json`）：**

```json
{
  "mcpServers": {
    "rider": {
      "type": "sse",
      "url": "http://localhost:9876/sse"
    }
  }
}
```

## 工具列表

### 连接管理

| 工具 | 说明 |
|------|------|
| `add_connection` | 添加数据库连接 |
| `update_connection` | 修改数据库连接 |
| `delete_connection` | 删除数据库连接 |
| `list_connections` | 列出所有已保存的连接 |

### 数据库操作

| 工具 | 说明 |
|------|------|
| `list_databases` | 列出所有数据库 |
| `create_database` | 创建新数据库（可选 charset） |
| `alter_database` | 重命名数据库 |
| `drop_database` | 删除数据库 ⚠️ |

### 表操作

| 工具 | 说明 |
|------|------|
| `list_tables` | 列出数据库中所有表（支持模糊搜索） |
| `describe_table` | 查看表字段结构 |
| `create_table` | 创建新表（支持字段定义数组） |
| `alter_table` | 修改表结构（add / drop / modify 字段） |
| `drop_table` | 删除表 |
| `truncate_table` | 清空表数据（保留结构） |

### 通用 SQL

| 工具 | 说明 |
|------|------|
| `query` | 执行 SELECT 查询，返回 JSON 结果 |
| `execute` | 执行 INSERT/UPDATE/DELETE/CREATE/DROP，返回影响行数 |

### 审计日志

| 工具 | 说明 |
|------|------|
| `audit_by_table` | 按表名查询所有操作记录 |
| `audit_recent` | 查看最近的审计日志 |

## 使用示例

### Cursor / Claude Desktop

```json
{
  "mcpServers": {
    "memento": {
      "type": "sse",
      "url": "http://localhost:9876/sse"
    }
  }
}
```

### curl

```bash
# 列出所有工具
curl -X POST http://localhost:9876/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# 查询表字段
curl -X POST http://localhost:9876/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"describe_table","arguments":{"connection_name":"tengxunyun","database_name":"miniprogram","table_name":"users"}}}'
```

## 项目结构

```
Memento.MCP/
├── Program.cs                           # 入口：ASP.NET Core + 自动注册
├── appsettings.json                     # 端口配置
├── Services/
│   ├── ConnectionManager.cs             # 连接字符串存储与 CRUD
│   ├── GlobalException.cs               # 全局异常捕获
│   ├── McpSseEndpoint.cs               # SSE/HTTP 端点处理
│   └── Util/
│       ├── ConnectionStringUtil.cs      # 连接串构建/提取
│       ├── DatabaseUtil.cs              # 数据库/表/字段 SQL 生成
│       ├── SqlSugarUtil.cs              # SqlSugar 工厂 + AOP 日志
│       ├── ToolResponse.cs              # MCP 响应快捷构造
│       ├── AuditLogger.cs               # SQL 审计日志
│       └── ColumnDef.cs                 # 字段定义模型
├── Register/
│   ├── McpTool.cs                       # 工具定义模型
│   ├── McpToolRegistry.cs              # 工具注册中心（反射自动扫）
│   ├── McpTypes.cs                     # 本地 MCP 类型定义
│   ├── Attributes/                     # 自定义属性：[McpToolset], [Tool], [McpBody], [McpParam]
│   └── Toolsets/
│       ├── ConnectionToolset.cs         # 连接管理
│       ├── DatabaseToolset.cs           # 数据库 CRUD
│       ├── TableToolset.cs             # 表 CRUD
│       ├── AuditToolset.cs             # 审计日志查询
│       └── Request/                    # Request DTO
│           ├── AddConnectionRequest.cs
│           ├── UpdateConnectionRequest.cs
│           ├── CreateDatabaseRequest.cs
│           ├── AlterDatabaseRequest.cs
│           ├── DescribeTableRequest.cs
│           ├── CreateTableRequest.cs
│           ├── ListTablesRequest.cs
│           ├── AlterTableRequest.cs
│           ├── DropTableRequest.cs
│           └── TruncateTableRequest.cs
├── logs/                                # 审计日志文件（自动生成）
└── UnitTest/
    ├── test_mcp_http.py                 # HTTP 自动化测试
    └── mcp_cli.py                       # 交互式测试终端
```

## 架构规范

- **参数限制：** 工具方法参数 ≥ 3 个时使用 `[McpBody]` + Request DTO
- **职责分离：** 业务逻辑在 `Services/Util/`，Toolset 只做校验 + 调 Util + 返响应
- **异常处理：** 所有 Toolset 方法无需 try-catch，异常由外层统一捕获并返回 `[ERR]` 响应
- **审计：** 所有 SQL 自动记录到 `logs/{连接名}_audit.jsonl`
