using GitLabNotify.Models;

namespace GitLabNotify.Data;

/// <summary>
/// Webhook 记录仓储接口
/// </summary>
/// <remarks>
/// 定义 Webhook 记录的持久化操作，由 SqliteRecordRepository 实现。
/// </remarks>
public interface IWebhookRecordRepository
{
    /// <summary>
    /// 插入一条 Webhook 记录
    /// </summary>
    /// <param name="record">待插入的记录</param>
    /// <returns>新插入记录的主键 ID</returns>
    Task<long> InsertAsync(WebhookRecord record);

    /// <summary>
    /// 更新指定记录的处理状态
    /// </summary>
    /// <param name="recordId">记录 ID</param>
    /// <param name="status">新状态</param>
    /// <param name="retryCount">已重试次数</param>
    /// <param name="error">错误信息（可为 null）</param>
    /// <param name="completedAt">完成时间（可为 null）</param>
    /// <returns>表示异步操作的任务</returns>
    Task UpdateStatusAsync(long recordId, WebhookStatus status, int retryCount, string? error, DateTime? completedAt);
}
