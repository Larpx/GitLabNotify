namespace Larpx.PersonalTools.GitLabNotify.Models
{
    /// <summary>
    /// Webhook 处理状态枚举
    /// </summary>
    public enum WebhookStatus
    {
        /// <summary>已接收，等待处理</summary>
        Pending = 0,

        /// <summary>处理中</summary>
        Processing = 1,

        /// <summary>处理成功</summary>
        Success = 2,

        /// <summary>处理失败（重试耗尽）</summary>
        Failed = 3,

        /// <summary>已跳过（无匹配目标）</summary>
        Skipped = 4
    }

    /// <summary>
    /// Webhook 记录实体（对应 SQLite 表 webhook_records）
    /// </summary>
    /// <remarks>
    /// 每条记录对应一次 GitLab Webhook 的接收和针对单个目标的转发结果。
    /// 若一次 Webhook 需要转发到多个目标，会生成多条记录。
    /// </remarks>
    public class WebhookRecord
    {
        /// <summary>主键 ID</summary>
        public long Id { get; set; }

        /// <summary>接收时间（UTC）</summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>事件类型（Push / MergeRequest 等）</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>原始 Payload（JSON 字符串）</summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>目标平台名称</summary>
        public string TargetName { get; set; } = string.Empty;

        /// <summary>目标平台类型</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>处理状态</summary>
        public WebhookStatus Status { get; set; } = WebhookStatus.Pending;

        /// <summary>已重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>错误信息（仅失败时）</summary>
        public string? Error { get; set; }

        /// <summary>处理完成时间（UTC）</summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// 待处理任务（在 Channel 中传递）
    /// </summary>
    public class WebhookTask
    {
        /// <summary>记录 ID（对应 webhook_records.id）</summary>
        public long RecordId { get; set; }

        /// <summary>事件类型</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>原始 Payload</summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>目标配置</summary>
        public TargetOptions Target { get; set; } = new();
    }
}
