namespace Larpx.PersonalTools.GitLabNotify.Services
{
    /// <summary>
    /// 事件格式化器接口
    /// </summary>
    /// <remarks>
    /// 负责将 GitLab Webhook 的原始 JSON Payload 转换为目标平台所需的消息格式。
    /// 每个目标平台对应一个实现。
    /// </remarks>
    public interface IEventFormatter
    {
        /// <summary>此格式化器支持的目标平台类型</summary>
        string TargetType { get; }

        /// <summary>
        /// 将原始 Payload 格式化为目标平台的消息体（JSON 字符串）
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="payload">原始 JSON Payload</param>
        /// <returns>目标平台消息体的 JSON 字符串；返回 null 时表示格式化失败（例如反序列化异常或不支持的事件类型），调用方应落库 Failed 而不发送</returns>
        string? Format(string eventType, string payload);
    }

    /// <summary>
    /// Webhook 目标平台适配器接口
    /// </summary>
    /// <remarks>
    /// 负责将格式化后的消息发送到目标平台的 Webhook URL。
    /// 每个目标平台对应一个实现。
    /// </remarks>
    public interface IWebhookTarget
    {
        /// <summary>此适配器支持的目标平台类型</summary>
        string TargetType { get; }

        /// <summary>
        /// 发送消息到目标平台
        /// </summary>
        /// <param name="url">目标平台 Webhook URL</param>
        /// <param name="message">已格式化的消息体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否发送成功</returns>
        Task<bool> SendAsync(string url, string message, CancellationToken cancellationToken = default);
    }
}
