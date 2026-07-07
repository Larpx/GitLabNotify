# GitLabNotify 项目设计说明

> 创建日期：2026-07-07
> 状态：已批准，进入实现阶段

## 一、项目背景

GitLab 老版本无法直接集成企业微信的转发插件，需要一个中间服务来：
1. 接收 GitLab 的 Webhook 回调
2. 解析事件内容
3. 将事件转发到企业微信等目标平台的 Webhook

部署方式：Docker 容器化。

## 二、技术选型

| 维度 | 选择 | 理由 |
|------|------|------|
| 框架 | .NET 10 + ASP.NET Core | 指定 |
| 接收端 | Kestrel + Controller | 轻量、原生 |
| 异步队列 | `System.Threading.Channels` | 内置、无外部依赖 |
| 持久化 | SQLite + Dapper | 单文件、Docker 友好、零运维 |
| 日志 | Serilog（控制台+文件） | 主流、结构化 |
| HTTP 客户端 | `IHttpClientFactory` | 原生、连接池管理 |
| 容器化 | 多阶段 Dockerfile + docker-compose | 镜像小、构建干净 |

## 三、目录结构

```
GitLabNotify/
├── src/GitLabNotify/
│   ├── Controllers/          # 接收 GitLab webhook
│   ├── Middleware/           # X-Gitlab-Token 校验
│   ├── Models/               # 事件、配置、记录模型
│   ├── Services/             # 管线、处理器、接口
│   ├── Targets/              # 目标平台适配器
│   ├── Formatters/           # 各平台消息格式化
│   ├── Data/                 # SQLite 仓储
│   ├── appsettings.json
│   ├── Program.cs
│   └── GitLabNotify.csproj
├── tests/GitLabNotify.Tests/
├── docker/
│   ├── Dockerfile
│   └── docker-compose.yml
├── docs/specs/
├── .gitignore
└── GitLabNotify.sln
```

## 四、数据流

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
                          └─ 落库 status=Success/Failed + 错误信息
```

关键点：接收和转发解耦。即使目标平台宕机，GitLab 仍然能收到 200 响应，不会因为转发故障导致 GitLab 重试或丢失 webhook。

## 五、配置结构（appsettings.json）

```json
{
  "GitLab": {
    "WebhookSecrets": ["secret-1", "secret-2"]
  },
  "Targets": [
    {
      "Name": "企业微信群机器人",
      "Type": "WechatWork",
      "Url": "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=xxx",
      "Enabled": true,
      "Events": ["Push"]
    }
  ],
  "Pipeline": {
    "MaxRetryCount": 3,
    "InitialRetryDelaySeconds": 5,
    "ChannelCapacity": 1000
  },
  "Persistence": {
    "ConnectionString": "Data Source=/data/gitlabnotify.db"
  }
}
```

环境变量覆盖（Docker 友好）：`GitLab__WebhookSecrets__0`、`Targets__0__Url` 等。

## 六、关键设计点

### 1. 适配器模式（目标平台扩展）
`IWebhookTarget` + `IEventFormatter` 双抽象。新增平台只需加两个文件并在 DI 注册，不改任何现有代码。

### 2. 失败重试
指数退避（5s → 10s → 20s），最多 3 次（可配）。重试期间记录在 SQLite 中可查，最终失败时记为 Failed 并保留原始 payload 用于人工补发。

### 3. Token 鉴权
通过中间件校验 `X-Gitlab-Token` 请求头与配置的 Secret 列表是否包含匹配项，不一致返回 401。配置支持多 Secret（兼容多 GitLab 实例）。

### 4. 健康检查
`/health` 端点，检查 SQLite 可连接 + Channel 未溢出。Docker `HEALTHCHECK` 使用此端点。

### 5. 日志留存
SQLite 表 `webhook_records`：
`id, received_at, event_type, payload, target_name, status, retry_count, error, completed_at`

便于事后排查和补发。

## 七、Docker 部署

- 多阶段构建：`mcr.microsoft.com/dotnet/sdk:10.0` 构建 → `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` 运行
- 暴露 8080 端口
- 挂载 `/data` 卷持久化 SQLite
- `docker-compose.yml` 含服务定义 + 健康检查 + 重启策略

## 八、开发阶段划分

1. 骨架搭建：解决方案、csproj、Program.cs、Dockerfile、docker-compose
2. 接收层：Controller + Auth 中间件 + Push 事件模型
3. 持久化：SQLite 仓储 + DbInitializer
4. 处理管线：Channel + BackgroundService + 重试逻辑
5. 企业微信适配器：Formatter + Target（先打通端到端）
6. 其余适配器：钉钉、飞书、通用 HTTP
7. Docker 化：多阶段 Dockerfile + compose + 健康检查
