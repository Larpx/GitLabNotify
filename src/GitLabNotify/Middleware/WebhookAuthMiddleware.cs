using GitLabNotify.Models;
using Microsoft.Extensions.Options;

namespace GitLabNotify.Middleware;

/// <summary>
/// GitLab Webhook Token 鉴权中间件
/// </summary>
/// <remarks>
/// 校验请求头 X-Gitlab-Token 是否在配置的 WebhookSecrets 列表中。
/// 仅对 /webhook 路径生效，其他路径（如 /health）直接放行。
/// 若配置的 Secret 列表为空，则跳过校验（仅用于调试，生产环境必须配置）。
/// </remarks>
public class WebhookAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebhookAuthMiddleware> _logger;

    /// <summary>Webhook 接收路径</summary>
    public const string WebhookPath = "/webhook";

    public WebhookAuthMiddleware(RequestDelegate next, ILogger<WebhookAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<GitLabOptions> gitLabOptions)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 仅对 /webhook 路径进行鉴权
        if (!path.StartsWith(WebhookPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var secrets = gitLabOptions.Value.WebhookSecrets;

        // 配置为空时跳过校验（仅用于调试）
        if (secrets == null || secrets.Count == 0)
        {
            _logger.LogWarning("未配置 GitLab Webhook Secret，跳过鉴权校验。生产环境必须配置！");
            await _next(context);
            return;
        }

        // 读取 X-Gitlab-Token 请求头
        if (!context.Request.Headers.TryGetValue("X-Gitlab-Token", out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Webhook 请求缺少 X-Gitlab-Token 头，来自 {RemoteIp}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing X-Gitlab-Token header.");
            return;
        }

        // 校验 Token 是否在配置列表中
        if (!secrets.Contains(token!))
        {
            _logger.LogWarning("Webhook 请求的 X-Gitlab-Token 无效，来自 {RemoteIp}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid X-Gitlab-Token.");
            return;
        }

        await _next(context);
    }
}
