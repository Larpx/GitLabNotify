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

**技术栈**：.NET 10 + ASP.NET Core + SQLite + Serilog + Docker

---

## 架构设计

采用**模块化单体 + 异步管线**架构，接收与转发解耦：

```
GitLab ──POST /webhook──► [AuthMiddleware 校验 Token]
                                │
                                ▼
                         WebhookController
                          ├─ 解析事件类型
                          ├─ 构造 WebhookRecord
                          ├─ Channel.Writer.WriteAsync  ◄── 立即返回 200
                          └─ 落库 status=Pending
                                │
                                ▼
                  WebhookPipeline (BackgroundService)
                          ├─ Channel.Reader 读取
                          └─ 交给 WebhookProcessor
                                │
                                ▼
                       WebhookProcessor
                          ├─ 按 target 配置循环
                          ├─ 选 Formatter 转换消息
                          ├─ 调 IWebhookTarget.SendAsync
                          ├─ 失败 → 指数退避重试（最多 N 次）
                          └─ 落库 status=Success/Failed
```

**关键设计**：即使目标平台故障，GitLab 仍然能收到 200 响应，不会因为转发故障导致 GitLab 重试或丢失 webhook。

详细设计文档见 [docs/specs/2026-07-07-gitlabnotify-design.md](docs/specs/2026-07-07-gitlabnotify-design.md)。

---

## 目录结构

```
GitLabNotify/
├── src/GitLabNotify/
│   ├── Controllers/              # Webhook 接收控制器
│   │   └── WebhookController.cs
│   ├── Middleware/               # 中间件
│   │   └── WebhookAuthMiddleware.cs    # X-Gitlab-Token 鉴权
│   ├── Models/                   # 数据模型
│   │   ├── GitLabEventType.cs    # 事件类型常量
│   │   ├── GitLabPushEvent.cs    # Push 事件模型
│   │   ├── WebhookRecord.cs      # 记录实体 + 任务模型
│   │   └── TargetOptions.cs      # 配置模型
│   ├── Services/                 # 核心服务
│   │   ├── IWebhookTarget.cs     # 目标/格式化器接口
│   │   ├── WebhookPipeline.cs    # 异步处理管线
│   │   ├── WebhookProcessor.cs   # 处理器（含重试）
│   │   └── WebhookDispatcher.cs  # 事件分发器
│   ├── Targets/                  # 目标平台适配器
│   │   └── HttpWebhookTarget.cs  # 通用 HTTP + 企业微信/钉钉/飞书
│   ├── Formatters/               # 消息格式化器
│   │   ├── WechatWorkFormatter.cs
│   │   ├── DingTalkFormatter.cs
│   │   └── FeishuFormatter.cs
│   ├── Data/                     # 数据访问层
│   │   ├── DbInitializer.cs      # SQLite 表结构初始化
│   │   └── SqliteRecordRepository.cs
│   ├── Program.cs                # 应用入口 + DI 注册
│   ├── appsettings.json          # 主配置
│   └── GitLabNotify.csproj
├── docker/
│   ├── Dockerfile                # 多阶段构建
│   ├── docker-compose.yml        # 容器编排
│   └── .env.example              # 环境变量示例
├── docs/specs/                   # 设计文档
├── .gitignore
└── GitLabNotify.sln
```

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

4. **Swagger 调试**：开发环境下访问 `http://localhost:5000/swagger`

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
| `GET` | `/` | 服务信息 |
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
  "timestamp": "2026-07-07T10:30:00.0000000Z"
}
```

---

## 支持的目标平台

| 平台 | 类型标识 | 消息格式 | 文档 |
|------|----------|----------|------|
| 企业微信群机器人 | `WechatWork` | Markdown | [文档](https://developer.work.weixin.qq.com/document/path/91770) |
| 钉钉群机器人 | `DingTalk` | Markdown | [文档](https://open.dingtalk.com/document/robots/custom-robot-access) |
| 飞书群机器人 | `Feishu` | 交互式卡片 | [文档](https://open.feishu.cn/document/client-docs/bot-v3/add-custom-bot) |
| 通用 HTTP | `Http` | 原样转发 | - |

### 扩展新目标平台

新增一个目标平台只需两步（**不改任何现有代码**）：

1. 在 `Formatters/` 添加实现 `IEventFormatter` 的类
2. 在 `Targets/` 添加实现 `IWebhookTarget` 的类（可继承 `HttpWebhookTarget`）
3. 在 `Program.cs` 的 DI 容器注册这两个类

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

**查询示例**：

```bash
sqlite3 data/gitlabnotify.db "SELECT id, received_at, event_type, target_name, status, retry_count FROM webhook_records ORDER BY id DESC LIMIT 20;"
```

---

## 运维

### 查看日志

- **控制台**：`docker logs -f gitlabnotify`
- **文件**：`docker/logs/gitlabnotify-YYYYMMDD.log`（按日滚动，保留 30 天）

### 健康检查

```bash
curl http://localhost:8080/health
# Healthy: 服务正常
# Unhealthy: SQLite 不可连接
```

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

- .NET 10 + ASP.NET Core
- SQLite + Dapper（持久化）
- Serilog（日志）
- System.Threading.Channels（异步队列）
- Swashbuckle（Swagger）

### 构建

```bash
dotnet build src/GitLabNotify/GitLabNotify.csproj -c Release
```

### 发布

```bash
dotnet publish src/GitLabNotify/GitLabNotify.csproj -c Release -o ./publish
```

### 项目结构说明

项目采用**分层 + 适配器模式**：
- **接收层**：`Controllers/` + `Middleware/`，仅负责接收和鉴权
- **处理层**：`Services/`，负责异步消费、重试、落库
- **适配层**：`Targets/` + `Formatters/`，每个目标平台一组实现
- **数据层**：`Data/`，SQLite 仓储

新增事件类型（如 MergeRequest）只需：
1. 在 `Models/` 添加事件模型
2. 在 `Models/GitLabEventType.cs` 添加类型常量
3. 在各 `Formatter` 中添加对应的格式化分支

---

## 许可证

私有项目，未授权不可使用。
