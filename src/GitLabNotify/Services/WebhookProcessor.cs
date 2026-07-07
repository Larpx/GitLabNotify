using GitLabNotify.Data;
using GitLabNotify.Models;
using Microsoft.Extensions.Options;

namespace GitLabNotify.Services;

/// <summary>
/// Webhook 处理器接口
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// 处理单个 Webhook 任务
    /// </summary>
    Task ProcessAsync(WebhookTask task, CancellationToken cancellationToken = default);
}

/// <summary>
/// Webhook 处理器实现
/// </summary>
/// <remarks>
/// 负责协调格式化、发送、重试和状态更新。
/// 重试策略：指数退避（initialDelay * 2^attempt），最多 MaxRetryCount 次。
/// </remarks>
public class WebhookProcessor : IWebhookProcessor
{
    private readonly IEnumerable<IEventFormatter> _formatters;
    private readonly IEnumerable<IWebhookTarget> _targets;
    private readonly IWebhookRecordRepository _repository;
    private readonly PipelineOptions _pipelineOptions;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IEnumerable<IEventFormatter> formatters,
        IEnumerable<IWebhookTarget> targets,
        IWebhookRecordRepository repository,
        IOptions<PipelineOptions> pipelineOptions,
        ILogger<WebhookProcessor> logger)
    {
        _formatters = formatters;
        _targets = targets;
        _repository = repository;
        _pipelineOptions = pipelineOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(WebhookTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始处理 Webhook 任务，记录 ID={RecordId}, 事件={EventType}, 目标={TargetName}",
            task.RecordId, task.EventType, task.Target.Name);

        // 更新状态为处理中
        await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Processing, 0, null, null);

        try
        {
            // 根据目标类型选择格式化器
            var formatter = _formatters.FirstOrDefault(f =>
                string.Equals(f.TargetType, task.Target.Type, StringComparison.OrdinalIgnoreCase));

            // 根据目标类型选择适配器
            var target = _targets.FirstOrDefault(t =>
                string.Equals(t.TargetType, task.Target.Type, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                var error = $"未找到目标平台适配器：{task.Target.Type}";
                _logger.LogError(error);
                await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Failed, 0, error, DateTime.UtcNow);
                return;
            }

            // 格式化消息（无格式化器时原样转发）
            var message = formatter?.Format(task.EventType, task.Payload) ?? task.Payload;

            // 执行发送 + 失败重试
            var maxRetry = Math.Max(0, _pipelineOptions.MaxRetryCount);
            var retryCount = 0;
            string? lastError = null;

            while (true)
            {
                try
                {
                    var success = await target.SendAsync(task.Target.Url, message, cancellationToken);
                    if (success)
                    {
                        _logger.LogInformation("Webhook 任务处理成功，记录 ID={RecordId}", task.RecordId);
                        await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Success, retryCount, null, DateTime.UtcNow);
                        return;
                    }

                    lastError = "目标适配器返回失败";
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    _logger.LogWarning(ex, "Webhook 发送失败，记录 ID={RecordId}, 重试次数={RetryCount}", task.RecordId, retryCount);
                }

                retryCount++;
                if (retryCount > maxRetry)
                {
                    break;
                }

                // 指数退避：initialDelay * 2^(retryCount-1)
                var delaySeconds = _pipelineOptions.InitialRetryDelaySeconds * Math.Pow(2, retryCount - 1);
                _logger.LogInformation("等待 {Delay}s 后重试，记录 ID={RecordId}, 第 {Retry} 次", delaySeconds, task.RecordId, retryCount);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Failed, retryCount, "服务停止，取消重试", DateTime.UtcNow);
                    throw;
                }

                await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Processing, retryCount, lastError, null);
            }

            // 重试耗尽，标记为失败
            _logger.LogError("Webhook 任务处理失败，记录 ID={RecordId}, 已重试 {Retry} 次", task.RecordId, retryCount);
            await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Failed, retryCount, lastError, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook 任务处理发生未预期异常，记录 ID={RecordId}", task.RecordId);
            await _repository.UpdateStatusAsync(task.RecordId, WebhookStatus.Failed, 0, ex.Message, DateTime.UtcNow);
        }
    }
}
