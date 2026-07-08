namespace Larpx.PersonalTools.GitLabNotify.Models
{
    /// <summary>
    /// 目标平台类型枚举（配置中的 Type 字段）
    /// </summary>
    public static class TargetType
    {
        /// <summary>企业微信群机器人</summary>
        public const string WechatWork = "WechatWork";

        /// <summary>钉钉群机器人</summary>
        public const string DingTalk = "DingTalk";

        /// <summary>飞书群机器人</summary>
        public const string Feishu = "Feishu";

        /// <summary>通用 HTTP Webhook（原样转发）</summary>
        public const string Http = "Http";
    }

    /// <summary>
    /// 目标平台配置（appsettings.json 中 Targets 数组的每一项）
    /// </summary>
    public class TargetOptions
    {
        /// <summary>目标名称（用于日志和记录中的标识）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>目标平台类型（见 <see cref="TargetType"/>）</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>目标 Webhook URL</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 订阅的事件类型列表
        /// </summary>
        /// <remarks>
        /// 空列表或 null 表示订阅所有事件。
        /// </remarks>
        public List<string>? Events { get; set; }

        /// <summary>
        /// 判断此目标是否订阅指定事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>订阅返回 true，否则 false</returns>
        public bool SubscribesTo(string eventType)
        {
            if (Events == null || Events.Count == 0)
            {
                return true;
            }

            return Events.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// GitLab 配置（appsettings.json 中 GitLab 节点）
    /// </summary>
    public class GitLabOptions
    {
        /// <summary>
        /// Webhook Secret Token 列表
        /// </summary>
        /// <remarks>
        /// 用于校验请求头 X-Gitlab-Token。
        /// 支持多个 Token 以兼容多个 GitLab 实例。
        /// 空列表表示不校验（仅用于调试，不推荐生产使用）。
        /// </remarks>
        public List<string> WebhookSecrets { get; set; } = new();
    }

    /// <summary>
    /// 管线配置（appsettings.json 中 Pipeline 节点）
    /// </summary>
    public class PipelineOptions
    {
        /// <summary>最大重试次数（默认 3）</summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>初始重试延迟秒数（默认 5）</summary>
        public int InitialRetryDelaySeconds { get; set; } = 5;

        /// <summary>Channel 容量（默认 1000，-1 表示无界）</summary>
        public int ChannelCapacity { get; set; } = 1000;
    }

    /// <summary>
    /// 持久化配置（appsettings.json 中 Persistence 节点）
    /// </summary>
    public class PersistenceOptions
    {
        /// <summary>SQLite 连接字符串</summary>
        public string ConnectionString { get; set; } = "Data Source=gitlabnotify.db";
    }
}
