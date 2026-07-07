using GitLabNotify.Data;
using GitLabNotify.Formatters;
using GitLabNotify.Middleware;
using GitLabNotify.Models;
using GitLabNotify.Services;
using GitLabNotify.Targets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

// ============================================================================
// GitLabNotify - GitLab Webhook 转发服务
// ============================================================================
// 接收 GitLab Webhook，格式化后转发到企业微信、钉钉、飞书等目标平台。
// 采用异步管线设计：接收端快速返回 200，由后台服务处理转发和重试。
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// --- 1. 配置 Serilog 日志 ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GitLabNotify"));

// --- 2. 绑定强类型配置 ---
builder.Services.Configure<GitLabOptions>(builder.Configuration.GetSection("GitLab"));
builder.Services.Configure<PipelineOptions>(builder.Configuration.GetSection("Pipeline"));
builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection("Persistence"));
builder.Services.Configure<List<TargetOptions>>(builder.Configuration.GetSection("Targets"));

// --- 3. 注册 HttpClient ---
builder.Services.AddHttpClient<HttpWebhookTarget>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitLabNotify/1.0");
});
builder.Services.AddHttpClient<WechatWorkTarget>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitLabNotify/1.0");
});
builder.Services.AddHttpClient<DingTalkTarget>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitLabNotify/1.0");
});
builder.Services.AddHttpClient<FeishuTarget>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitLabNotify/1.0");
});

// --- 4. 注册仓储 ---
// 注意：连接字符串延迟到 app.Build() 后从合并后的配置读取
// 这样 WebApplicationFactory 注入的配置才能生效
builder.Services.AddSingleton<IWebhookRecordRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config.GetSection("Persistence:ConnectionString").Get<string>()
        ?? new PersistenceOptions().ConnectionString;
    return new SqliteRecordRepository(connStr, sp.GetRequiredService<ILogger<SqliteRecordRepository>>());
});

// --- 5. 注册管线与处理器 ---
builder.Services.AddSingleton<WebhookPipeline>();
builder.Services.AddSingleton<IWebhookProcessor, WebhookProcessor>();
builder.Services.AddSingleton<WebhookDispatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookPipeline>());

// --- 6. 注册格式化器（适配器模式：每个目标平台一个） ---
builder.Services.AddSingleton<IEventFormatter, WechatWorkFormatter>();
builder.Services.AddSingleton<IEventFormatter, DingTalkFormatter>();
builder.Services.AddSingleton<IEventFormatter, FeishuFormatter>();

// --- 7. 注册目标适配器（适配器模式：每个目标平台一个） ---
builder.Services.AddSingleton<IWebhookTarget, HttpWebhookTarget>();
builder.Services.AddSingleton<IWebhookTarget, WechatWorkTarget>();
builder.Services.AddSingleton<IWebhookTarget, DingTalkTarget>();
builder.Services.AddSingleton<IWebhookTarget, FeishuTarget>();

// --- 8. 注册控制器与 Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- 9. 注册健康检查 ---
// 健康检查的连接字符串也延迟读取
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("OK"))
    .AddSqlite(
        sp => sp.GetRequiredService<IConfiguration>().GetSection("Persistence:ConnectionString").Get<string>()
            ?? new PersistenceOptions().ConnectionString,
        name: "sqlite",
        failureStatus: HealthStatus.Unhealthy);

// --- 10. 构建应用 ---
var app = builder.Build();

// --- 11. 初始化数据库 ---
// 此时 app.Configuration 已包含所有配置源（包括 WebApplicationFactory 注入的）
var persistenceConnStr = app.Configuration.GetSection("Persistence:ConnectionString").Get<string>()
    ?? new PersistenceOptions().ConnectionString;
try
{
    Log.Information("正在初始化 SQLite 数据库...");
    DbInitializer.Initialize(persistenceConnStr);
    Log.Information("SQLite 数据库初始化完成，连接字符串：{ConnectionString}", persistenceConnStr);
}
catch (Exception ex)
{
    Log.Fatal(ex, "SQLite 数据库初始化失败");
    throw;
}

// --- 12. 配置中间件管道 ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<WebhookAuthMiddleware>();

app.MapControllers();

// 健康检查端点
app.MapHealthChecks("/health");

// 根路径返回简单信息（方便验证服务是否启动）
app.MapGet("/", () => new
{
    name = "GitLabNotify",
    version = "1.0.0",
    description = "GitLab Webhook 转发服务",
    endpoints = new
    {
        webhook = "/webhook",
        health = "/health",
        swagger = "/swagger"
    }
});

// --- 13. 启动应用 ---
Log.Information("GitLabNotify 服务启动，监听端口：{Port}", builder.Configuration["ASPNETCORE_URLS"] ?? "默认");
app.Run();

// 将 Program 声明为 partial，以便集成测试项目使用 WebApplicationFactory<Program>
public partial class Program { }
