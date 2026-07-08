using Larpx.PersonalTools.GitLabNotify.Models;

namespace Larpx.PersonalTools.GitLabNotify.Data
{
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

        /// <summary>
        /// 获取最近的 Webhook 记录
        /// </summary>
        /// <param name="count">返回的最大记录数；为 null 时返回所有记录</param>
        /// <returns>按接收时间倒序的记录集合</returns>
        Task<IEnumerable<WebhookRecord>> GetRecentAsync(int? count = null);
    }
}
