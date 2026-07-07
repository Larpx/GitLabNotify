using System.Threading.Channels;
using GitLabNotify.Data;
using GitLabNotify.Models;
using Microsoft.Extensions.Options;

namespace GitLabNotify.Services;

/// <summary>
/// Webhook 异步处理管线
/// </summary>
/// <remarks>
/// 基于 System.Threading.Channels 实现的内存队列。
/// 接收端写入 Channel 后立即返回，由 BackgroundService 异步消费并转发。
/// 这样即使目标平台故障，也不会阻塞 GitLab 的 Webhook 请求。
/// </remarks>
public class WebhookPipeline : BackgroundService
{
    private readonly Channel<WebhookTask> _channel;
    private readonly IWebhookProcessor _processor;
    private readonly ILogger<WebhookPipeline> _logger;

    /// <summary>当前队列中的待处理任务数</summary>
    public int PendingCount => _channel.Reader.Count;

    public WebhookPipeline(IWebhookProcessor processor, IOptions<PipelineOptions> options, ILogger<WebhookPipeline> logger)
    {
        _processor = processor;
        _logger = logger;

        var capacity = options.Value.ChannelCapacity;

        _channel = Channel.CreateBounded<WebhookTask>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// 将任务写入管线
    /// </summary>
    /// <param name="task">待处理任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task EnqueueAsync(WebhookTask task, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(task, cancellationToken);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook 处理管线已启动");

        try
        {
            await foreach (var task in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _processor.ProcessAsync(task, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 正常关闭
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 Webhook 任务时发生未预期异常，记录 ID={RecordId}", task.RecordId);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Webhook 处理管线已停止");
        }
    }
}
