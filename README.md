# GitLabNotify

> GitLab Webhook 转发服务 —— 接收 GitLab Webhook，格式化后转发到企业微信、钉钉、飞书等目标平台。

## 项目简介

GitLabNotify 是一个轻量级的 Webhook 中继服务，专为 GitLab 老版本无法直接集成企业微信转发插件的场景设计。

**核心能力**：
- 接收 GitLab 的 Webhook 回调（Push 事件，架构可扩展其他事件）
- 将事件内容格式化为目标平台所需的卡片消息
- 转发到企业微信群机器人、钉钉群机器人、飞书群机器人等目标平台
- 失败自动重试（指数退避）
- 全量日志留存到 SQLite，便于排查和补发
- Token 鉴权防止伪造请求
- 健康检查端点供 Docker 监控
- 全局异常处理：未捕获异常返回统一 500 响应，绝不暴露内部错误详情
- 关键不变量：**错误消息绝不外发到目标平台**，格式化失败或不支持事件类型时落库 `Failed` 而非推送

**技术栈**：.NET 10 + ASP.NET Core + SQLite + Serilog + Docker

**命名空间根**：`Larpx.PersonalTools.GitLabNotify`

---

## 架构设计

采用**模块化单体 + 异步管线**架构，接收与转发解耦：

```
GitLab ──POST /webhook──► [GlobalExceptionMiddleware]  ◄── 管道最外层,统一捕获未处理异常
                                │
                                ▼
                       [WebhookAuthMiddleware 校验 X-Gitlab-Token]
                                │
                                ▼
                         WebhookController
                          ├─ 读取原始 Body
                          ├─ 解析事件类型
                          ├─ 构造 WebhookRecord (status=Pending)
                          └─ Dispatcher.DispatchAsync         ◄── 立即返回 200
                                │
                                ▼
                  WebhookPipeline (BackgroundService)
                          ├─ Channel.Reader.ReadAllAsync
                          ├─ 异常 catch 兜底落库 Failed
                          └─ 交给 WebhookProcessor
                                │
                                ▼
                       WebhookProcessor
                          ├─ 选 Formatter (匹配 target.Type)
                          ├─ 选 Target (匹配 target.Type)
                          ├─ formatter.Format(eventType, payload)
                          │     └─ 返回 null → 落库 Failed,return,绝不发送
                          ├─ 重试循环: target.SendAsync (指数退避,最多 N 次)
                          └─ 落库 status=Success/Failed
```

**关键设计**：
- 即使目标平台故障，GitLab 仍然能收到 200 响应，不会因为转发故障导致 GitLab 重试或丢失 webhook。
- **错误消息绝不外发**：Formatter 在反序列化失败、不支持事件类型时返回 `null`，Processor 检测到 `null` 直接落库 `Failed` 并 `return`，**不会**调用 `target.SendAsync`，避免把 "解析失败 / 不支持" 这类内部状态作为消息推送到企业微信/钉钉/飞书群。
- **全局异常隔离**：`GlobalExceptionMiddleware` 是管道最外层，下游任何未捕获异常都被它记录到 Serilog 并返回 `{"error":"Internal Server Error"}`，响应体不包含 `ex.Message`。

详细设计文档见 [docs/specs/2026-07-07-gitlabnotify-design.md](docs/specs/2026-07-07-gitlabnotify-design.md)。

---

## 目录结构

```
GitLabNotify/
├── src/GitLabNotify/
│   ├── Controllers/                       # Webhook 接收控制器
│   │   └── WebhookController.cs
│   ├── Middleware/                        # 中间件
│   │   ├── GlobalExceptionMiddleware.cs   # 全局异常处理(管道最外层)
│   │   └── WebhookAuthMiddleware.cs       # X-Gitlab-Token 鉴权
│   ├── Models/                            # 数据模型
│   │   ├── GitLabEventType.cs             # 事件类型常量
│   │   ├── GitLabPushEvent.cs             # Push 事件模型
│   │   ├── WebhookRecord.cs               # 记录实体 + 任务模型
│   │   └── TargetOptions.cs               # 配置模型 (GitLabOptions/PipelineOptions/PersistenceOptions)
│   ├── Services/                          # 核心服务
│   │   ├── IWebhookTarget.cs              # IEventFormatter / IWebhookTarget 接口
│   │   ├── WebhookPipeline.cs             # 异步处理管线(Channel 队列)
│   │   ├── WebhookProcessor.cs            # 处理器(含重试 + 错误拦截)
│   │   └── WebhookDispatcher.cs           # 事件分发器
│   ├── Targets/                           # 目标平台适配器
│   │   └── HttpWebhookTarget.cs           # 通用 HTTP + 企业微信/钉钉/飞书
│   ├── Formatters/                        # 消息格式化器
│   │   ├── WechatWorkFormatter.cs
│   │   ├── DingTalkFormatter.cs
│   │   └── FeishuFormatter.cs
│   ├── Data/                              # 数据访问层
│   │   ├── DbInitializer.cs               # SQLite 表结构初始化
│   │   ├── IWebhookRecordRepository.cs    # 仓储接口
│   │   └── SqliteRecordRepository.cs      # Dapper 实现
│   ├── Program.cs                         # 应用入口(Main + DI 注册 + 中间件管道)
│   ├── appsettings.json                   # 主配置
│   └── GitLabNotify.csproj
├── tests/
│   └── GitLabNotify.Tests/                # 单元测试 + 集成测试(xUnit)
├── docker/
│   ├── Dockerfile                         # 多阶段构建
│   ├── docker-compose.yml                 # 容器编排
│   └── .env.example                       # 环境变量示例
├── docs/specs/                            # 设计文档
├── .gitignore
└── GitLabNotify.slnx                      # 解决方案文件
```

**所有源代码命名空间**：`Larpx.PersonalTools.GitLabNotify.{Controllers,Middleware,Models,Services,Targets,Formatters,Data}`

---

## 快速开始

### 方式一：Docker Compose 部署（推荐）

1. **克隆仓库并配置环境变量**

   ```bash
   git clone <repo-url>
   cd GitLabNotify
   cp docker/.env.example docker/.env
   ```

2. **编辑 `docker/.env` 填写配置**

   ```env
   GITLAB_WEBHOOK_SECRET=your-secret-token
   TARGET_0_URL=https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=your-wechat-work-key
   ```

3. **启动服务**

   ```bash
   cd docker
   docker-compose up -d
   ```

4. **验证服务**

   ```bash
   curl http://localhost:8080/health
   # 返回 Healthy 表示正常
   ```

### 方式二：本地运行（开发调试）

1. **环境要求**：.NET 10 SDK

2. **修改配置**：编辑 [src/GitLabNotify/appsettings.json](src/GitLabNotify/appsettings.json)，配置 `GitLab.WebhookSecrets` 和 `Targets`

3. **运行**

   ```bash
   cd src/GitLabNotify
   dotnet run
   ```

4. **Swagger 调试**：开发环境（`ASPNETCORE_ENVIRONMENT=Development`）访问 `http://localhost:5000/swagger`（端口由 `ASPNETCORE_URLS` 决定；docker 默认 8080）

---

## 配置说明

配置通过 `appsettings.json` 或环境变量提供。**Docker 部署推荐用环境变量**。

### 完整配置项

| 配置项 | 环境变量 | 说明 | 默认值 |
|--------|----------|------|--------|
| `GitLab:WebhookSecrets` | `GitLab__WebhookSecrets__0` | GitLab Webhook Token 列表（多实例可配多个） | 空（不校验） |
| `Targets:0:Name` | `Targets__0__Name` | 目标平台名称 | 企业微信群机器人 |
| `Targets:0:Type` | `Targets__0__Type` | 目标类型：WechatWork/DingTalk/Feishu/Http | WechatWork |
| `Targets:0:Url` | `Targets__0__Url` | 目标平台 Webhook URL | 空 |
| `Targets:0:Enabled` | `Targets__0__Enabled` | 是否启用 | true |
| `Targets:0:Events` | - | 订阅的事件类型列表（空=全部） | ["Push"] |
| `Pipeline:MaxRetryCount` | `Pipeline__MaxRetryCount` | 最大重试次数 | 3 |
| `Pipeline:InitialRetryDelaySeconds` | `Pipeline__InitialRetryDelaySeconds` | 初始重试延迟（秒） | 5 |
| `Pipeline:ChannelCapacity` | `Pipeline__ChannelCapacity` | 队列容量 | 1000 |
| `Persistence:ConnectionString` | `Persistence__ConnectionString` | SQLite 连接字符串 | Data Source=data/gitlabnotify.db |
| `Serilog:MinimumLevel:Default` | `Serilog__MinimumLevel__Default` | 日志级别：Verbose/Debug/Information/Warning/Error/Fatal | Information |

### 多目标配置示例

在 `appsettings.json` 中配置多个目标：

```json
{
  "Targets": [
    {
      "Name": "企业微信-研发群",
      "Type": "WechatWork",
      "Url": "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=key1",
      "Enabled": true,
      "Events": ["Push"]
    },
    {
      "Name": "钉钉-通知群",
      "Type": "DingTalk",
      "Url": "https://oapi.dingtalk.com/robot/send?access_token=token1",
      "Enabled": true,
      "Events": ["Push"]
    }
  ]
}
```

### Serilog 配置

`appsettings.json` 中已配置双 sink（控制台 + 按天滚动文件）。如需调整日志级别或输出格式，参考 [Serilog 设置参考](https://github.com/serilog/serilog-settings-configuration)。

---

## GitLab 端配置

1. 进入 GitLab 项目 → **Settings** → **Webhooks**
2. **URL** 填写：`http://your-server:8080/webhook`
3. **Secret Token** 填写：与 `GitLab:WebhookSecrets` 配置一致的值
4. **Trigger** 勾选：Push events
5. 点击 **Add webhook**，可用 **Test** 按钮测试推送

---

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| `POST` | `/webhook` | 接收 GitLab Webhook（需 X-Gitlab-Token 头） |
| `GET` | `/health` | 健康检查端点（供 Docker HEALTHCHECK 使用） |
| `GET` | `/` | 服务信息（JSON: name/version/description/endpoints） |
| `GET` | `/swagger` | Swagger UI（仅开发环境） |

### Webhook 请求示例

```bash
curl -X POST http://localhost:8080/webhook \
  -H "Content-Type: application/json" \
  -H "X-Gitlab-Token: your-secret-token" \
  -d '{
    "object_kind": "push",
    "ref": "refs/heads/main",
    "user_name": "张三",
    "project": {
      "name": "my-project",
      "web_url": "https://gitlab.com/mygroup/my-project",
      "path_with_namespace": "mygroup/my-project"
    },
    "commits": [
      {
        "id": "abc123def456",
        "message": "fix: 修复登录问题",
        "url": "https://gitlab.com/mygroup/my-project/-/commit/abc123def456",
        "author": { "name": "张三", "email": "zhangsan@example.com" }
      }
    ],
    "total_commits_count": 1
  }'
```

成功响应：

```json
{
  "received": true,
  "eventType": "Push",
  "targets": 1,
  "timestamp": "<UTC ISO 8601 时间戳>"
}
```

### 全局异常响应

任何未捕获异常（如 SQLite 写入失败、JSON 序列化异常）都会经 `GlobalExceptionMiddleware` 处理：

```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/json; charset=utf-8

{"error":"Internal Server Error"}
```

完整的异常详情（含堆栈）记录到 Serilog 日志，**响应体不包含 `ex.Message`**，避免内部信息泄漏。

### 关键不变量：错误消息绝不外发

当 Formatter 反序列化失败、遇到不支持的事件类型、或目标平台没有匹配的目标适配器时：

- Webhook 记录被标记为 `status=Failed`，`error` 字段写明原因
- **不会**调用 `target.SendAsync`，**不会**向企业微信/钉钉/飞书发送任何消息
- 日志中可见 `Webhook 任务格式化失败,记录 ID=...,消息格式化结果为空...` 字样

---

## 支持的目标平台

| 平台 | 类型标识 | 消息格式 | 文档 |
|------|----------|----------|------|
| 企业微信群机器人 | `WechatWork` | Markdown | [文档](https://developer.work.weixin.qq.com/document/path/91770) |
| 钉钉群机器人 | `DingTalk` | Markdown | [文档](https://open.dingtalk.com/document/robots/custom-robot-access) |
| 飞书群机器人 | `Feishu` | 交互式卡片 | [文档](https://open.feishu.cn/document/client-docs/bot-v3/add-custom-bot) |
| 通用 HTTP | `Http` | 原样转发 | - |

### 扩展新目标平台

新增一个目标平台只需三步（**不改任何现有代码**）：

1. 在 `Formatters/` 添加实现 `IEventFormatter` 的类（`TargetType` 返回平台标识）
2. 在 `Targets/` 添加实现 `IWebhookTarget` 的类（可继承 `HttpWebhookTarget`）
3. 在 `Program.cs` 的 `BuildApp` 方法内注册这两个类到 DI 容器

---

## 数据留存

所有 Webhook 接收和转发记录均存储在 SQLite 数据库中，表 `webhook_records`：

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER | 主键 |
| received_at | TEXT | 接收时间（UTC） |
| event_type | TEXT | 事件类型 |
| payload | TEXT | 原始 Payload |
| target_name | TEXT | 目标名称 |
| target_type | TEXT | 目标类型 |
| status | INTEGER | 状态：0=Pending 1=Processing 2=Success 3=Failed 4=Skipped |
| retry_count | INTEGER | 重试次数 |
| error | TEXT | 错误信息 |
| completed_at | TEXT | 完成时间 |

表结构由 `Data/DbInitializer.cs` 在应用启动时创建（`IF NOT EXISTS` 幂等）。三个索引：`(status)`, `(event_type)`, `(received_at)`。

**查询示例**：

```bash
sqlite3 data/gitlabnotify.db "SELECT id, received_at, event_type, target_name, status, retry_count FROM webhook_records ORDER BY id DESC LIMIT 20;"
```

---

## 运维

### 查看日志

- **控制台**：`docker logs -f gitlabnotify`
- **文件**：`docker/logs/gitlabnotify-YYYYMMDD.log`（按日滚动，保留 30 天）
- **结构化字段**：`EventType`、`RecordId`、`TargetName`、`Method`、`Path`、`RemoteIp` 等

### 健康检查

```bash
curl http://localhost:8080/health
# Healthy: 服务正常
# Unhealthy: SQLite 不可连接
```

Docker 部署已自动配置 HEALTHCHECK（`wget http://localhost:8080/health`），容器会以 30s 间隔自检。

### 重启服务

```bash
cd docker
docker-compose restart
```

### 升级

```bash
cd docker
docker-compose down
docker-compose build
docker-compose up -d
```

---

## 开发

### 技术栈

- .NET 10 + ASP.NET Core（显式 `Main` 入口，无顶级语句）
- SQLite + Dapper（持久化）
- Serilog（结构化日志）
- System.Threading.Channels（异步队列）
- Swashbuckle（Swagger）
- xUnit + FluentAssertions + Moq（测试）
- WebApplicationFactory<Program>（集成测试）

### 构建

```bash
dotnet build GitLabNotify.slnx -c Release
```

### 发布

```bash
dotnet publish src/GitLabNotify/GitLabNotify.csproj -c Release -o ./publish
```

### 测试

```bash
dotnet test GitLabNotify.slnx -c Release
```

当前测试覆盖：Formatters（WechatWork/DingTalk/Feishu）、Services（Processor 重试逻辑、Dispatcher 队列）、Middleware（鉴权）、Models、Integration（端到端含 MockServer）。

### 项目结构说明

项目采用**分层 + 适配器模式**：
- **接收层**：`Controllers/` + `Middleware/`，仅负责接收和鉴权
- **处理层**：`Services/`，负责异步消费、重试、落库
- **适配层**：`Targets/` + `Formatters/`，每个目标平台一组实现
- **数据层**：`Data/`，SQLite 仓储（接口 + Dapper 实现）

新增事件类型（如 MergeRequest）只需：
1. 在 `Models/` 添加事件模型
2. 在 `Models/GitLabEventType.cs` 添加类型常量
3. 在各 `Formatter` 中添加对应的格式化分支

### 添加新中间件

中间件风格参考 `WebhookAuthMiddleware.cs`：
- 命名空间 `Larpx.PersonalTools.GitLabNotify.Middleware`
- 字段一律 `private readonly`，`_next` + `_logger`
- 类注释三行式 `<summary>` + `<remarks>`
- `InvokeAsync` 直接处理 `HttpContext`，失败时 `WriteAsync` 简单文本
- 异常处理交给 `GlobalExceptionMiddleware`（除非中间件本身要做鉴权短路）

---

## 许可证

私有项目，未授权不可使用。
