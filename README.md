# Memento.MCP

**给你的 AI 一双手，直接操作数据库。**

Memento.MCP 是一个 MCP (Model Context Protocol) 服务器，让 Cursor、Claude Desktop、Windsurf、Rider 等 AI 编程工具直接连接并操作你的数据库——查结构、写 SQL、建表、分析数据，全交给 AI 替你完成。

```json
// 只需一行配置，AI 即刻获得数据库能力
{
  "mcpServers": {
    "memento": {
      "type": "sse",
      "url": "http://localhost:9876/sse"
    }
  }
}
```

---

## 效果

以前你问 AI "这个表有什么字段"，AI 只能回答"我无法直接查看你的数据库"。

现在 AI 可以：

> **你：** 帮我查一下 users 表的结构和最近注册的 10 个用户  
> **AI：** 好的，我查一下。  
> → 调用 `describe_table` 获取字段结构  
> → 调用 `query` 执行 `SELECT * FROM users ORDER BY created_at DESC LIMIT 10`  
> → 返回结果给你

AI 能替你做的不止这些——18 个工具覆盖了数据库管理的方方面面。

---

## 快速开始

### 1. 启动服务

```bash
Memento.MCP.exe
```

默认端口 9876，可从 `appsettings.json` 修改：

```json
{
  "Port": 9876
}
```

### 2. 添加数据库连接

服务启动后，让 AI 帮你添加连接：

> **你对 AI 说：** 添加一个 MySQL 连接，叫 "production"，地址 192.168.1.100:3306，用户 root

AI 会自动调用 `add_connection` 工具完成配置。

或者你自己在 Rider 交互终端添加（运行 `python.exe UnitTest/mcp_cli.py`）。

### 3. 开始使用

连接添加完成后，AI 就能替你执行所有数据库操作：

| 你想做什么 | 对 AI 说 |
|-----------|---------|
| 查看有哪些数据库 | "列出所有数据库" |
| 查看表结构 | "看看 users 表的结构" |
| 搜索表 | "有哪些表跟订单相关？" |
| 查数据 | "查一下最近 10 条订单" |
| 建表 | "帮我建一个文章表，有 id、标题、内容、发布时间" |
| 改表 | "给 users 表加一个 avatar 字段" |
| 分析数据 | "统计每个月的注册用户数" |

---

## AI 客户端接入

### Cursor

在项目根目录创建 `.cursor/mcp.json`：

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

### Claude Desktop

编辑 `claude_desktop_config.json`：

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

### Windsurf

在 `.codeium/windsurf/mcp_config.json` 中添加：

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

### JetBrains Rider

在项目根目录创建 `.idea/mcp.json`：

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

---

## 全部工具一览

### 连接管理

| 工具 | 用途 |
|------|------|
| `add_connection` | 让 AI 保存数据库连接（地址、端口、账号、密码） |
| `list_connections` | AI 查看已保存了哪些数据库连接 |
| `update_connection` | 修改已保存的连接信息 |
| `delete_connection` | 删除连接 |

### 数据库操作

| 工具 | 用途 |
|------|------|
| `list_databases` | AI 列出服务器上所有数据库 |
| `create_database` | AI 帮你创建新数据库 |
| `alter_database` | 重命名数据库（MySQL 自动迁移表） |
| `drop_database` | 删除数据库 ⚠️ |

### 表操作

| 工具 | 用途 |
|------|------|
| `list_tables` | 列出所有表（支持模糊搜索） |
| `describe_table` | 查看表字段、类型、注释 |
| `create_table` | AI 根据你的描述自动建表 |
| `alter_table` | 加字段、删字段、改字段类型 |
| `drop_table` | 删除表 |
| `truncate_table` | 清空表数据 |

### 数据查询

| 工具 | 用途 |
|------|------|
| `query` | AI 执行 SELECT，返回 JSON 结果 |
| `execute` | 执行 INSERT/UPDATE/DELETE，返回影响行数 |

### 审计

| 工具 | 用途 |
|------|------|
| `audit_by_table` | 查某张表被 AI 操作过哪些 |
| `audit_recent` | 查看最近的 SQL 执行记录 |

---

## 场景示例

### 场景 1：AI 协助分析数据

> **你：** 分析一下过去三个月销售额最高的 10 个商品  
> **AI：** 我先看看 orders 表的结构和有哪些相关表。  
> → 调用 `describe_table`、`list_tables`  
> → 调用 `query` 执行分析 SQL  
> → 给你一份完整的分析报告

### 场景 2：AI 帮你建表

> **你：** 帮我建一个博客系统需要的表，有用户、文章、分类、标签  
> **AI：** 创建 4 张表，包括字段类型、索引、外键关系  
> → 调用 4 次 `create_table`

### 场景 3：AI 排查数据库问题

> **你：** 为什么用户表查询这么慢？  
> **AI：** 查看表结构和数据量，分析索引情况，给出优化建议  
> → 调用 `describe_table`、`query` 查看表信息

---

## 技术栈

| 技术 | 用途 |
|------|------|
| .NET 10 + ASP.NET Core | Web 服务器 & SSE 传输 |
| ModelContextProtocol | 官方 MCP SDK |
| SqlSugarCore | ORM（MySQL / SqlServer / PostgreSQL / Oracle） |
| Streamable HTTP | SSE 端点 + POST JSON-RPC |

## 项目结构

```
Memento.MCP/
├── Program.cs                   # 入口
├── appsettings.json             # 端口配置
├── Services/                    # 业务层
│   └── Util/                    # SQL 生成、工具函数
├── Register/                    # MCP 框架层
│   ├── Attributes/              # [Tool], [McpBody], [McpParam]
│   └── Toolsets/                # 工具集合（4 个）
├── logs/                        # 审计日志
└── UnitTest/                    # 测试工具
```

## 许可

MIT
