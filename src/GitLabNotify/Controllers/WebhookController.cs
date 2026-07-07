using GitLabNotify.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitLabNotify.Controllers;

/// <summary>
/// Webhook 接收控制器
/// </summary>
/// <remarks>
/// 接收 GitLab 推送的 Webhook，解析事件类型后异步分发到匹配的目标平台。
/// </remarks>
[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    private readonly WebhookDispatcher _dispatcher;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(WebhookDispatcher dispatcher, ILogger<WebhookController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// 接收 GitLab Webhook
    /// </summary>
    /// <remarks>
    /// 接收原始 JSON Body，解析事件类型后立即异步分发，快速返回 200。
    /// GitLab 不需要等待转发完成，避免因目标平台故障导致 GitLab 重试。
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        // 读取原始 Body
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("收到空的 Webhook Payload，来自 {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return BadRequest("Empty payload.");
        }

        // 解析事件类型
        var eventType = WebhookDispatcher.ParseEventType(payload);
        _logger.LogInformation("收到 GitLab Webhook，事件类型={EventType}, 长度={Length}", eventType, payload.Length);

        // 异步分发
        var targetCount = await _dispatcher.DispatchAsync(eventType, payload, HttpContext.RequestAborted);

        return Ok(new
        {
            received = true,
            eventType,
            targets = targetCount,
            timestamp = DateTime.UtcNow
        });
    }
}
