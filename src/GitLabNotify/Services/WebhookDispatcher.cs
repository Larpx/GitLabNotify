using System.Text.Json;
using GitLabNotify.Data;
using GitLabNotify.Models;
using Microsoft.Extensions.Options;

namespace GitLabNotify.Services;

/// <summary>
/// Webhook 分发服务
/// </summary>
/// <remarks>
/// 在 Controller 接收 Webhook 后调用，负责：
/// 1. 解析事件类型
/// 2. 遍历启用的目标，按事件订阅筛选
/// 3. 为每个匹配的目标创建 WebhookRecord 并落库
/// 4. 将任务写入 WebhookPipeline 异步处理
/// </remarks>
public class WebhookDispatcher
{
    private readonly WebhookPipeline _pipeline;
    private readonly IWebhookRecordRepository _repository;
    private readonly IEnumerable<TargetOptions> _targets;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        WebhookPipeline pipeline,
        IWebhookRecordRepository repository,
        IOptions<List<TargetOptions>> targets,
        ILogger<WebhookDispatcher> logger)
    {
        _pipeline = pipeline;
        _repository = repository;
        _targets = targets.Value ?? new List<TargetOptions>();
        _logger = logger;
    }

    /// <summary>
    /// 分发 Webhook 到所有匹配的目标
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="payload">原始 JSON Payload</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分发到的目标数量</returns>
    public async Task<int> DispatchAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        var matchedTargets = _targets
            .Where(t => t.Enabled && t.SubscribesTo(eventType))
            .ToList();

        if (matchedTargets.Count == 0)
        {
            _logger.LogInformation("没有目标订阅事件 {EventType}", eventType);
            return 0;
        }

        _logger.LogInformation("分发事件 {EventType} 到 {Count} 个目标", eventType, matchedTargets.Count);

        foreach (var target in matchedTargets)
        {
            // 创建记录并落库
            var record = new WebhookRecord
            {
                ReceivedAt = DateTime.UtcNow,
                EventType = eventType,
                Payload = payload,
                TargetName = target.Name,
                TargetType = target.Type,
                Status = WebhookStatus.Pending,
                RetryCount = 0
            };

            var recordId = await _repository.InsertAsync(record);

            // 入队异步处理
            var task = new WebhookTask
            {
                RecordId = recordId,
                EventType = eventType,
                Payload = payload,
                Target = target
            };

            await _pipeline.EnqueueAsync(task, cancellationToken);
        }

        return matchedTargets.Count;
    }

    /// <summary>
    /// 从原始 Payload 中解析事件类型
    /// </summary>
    /// <param name="payload">JSON Payload</param>
    /// <returns>事件类型字符串</returns>
    public static string ParseEventType(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("object_kind", out var kind))
            {
                var kindStr = kind.GetString();
                return kindStr switch
                {
                    "push" => GitLabEventType.Push,
                    "merge_request" => GitLabEventType.MergeRequest,
                    "pipeline" => GitLabEventType.Pipeline,
                    "tag_push" => GitLabEventType.TagPush,
                    "issue" => GitLabEventType.Issue,
                    "note" => GitLabEventType.Note,
                    _ => kindStr ?? "Unknown"
                };
            }
        }
        catch (Exception)
        {
            // 忽略解析错误
        }

        return "Unknown";
    }
}
