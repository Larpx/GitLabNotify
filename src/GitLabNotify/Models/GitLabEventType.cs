namespace Larpx.PersonalTools.GitLabNotify.Models
{
    /// <summary>
    /// GitLab Webhook 事件类型枚举
    /// </summary>
    /// <remarks>
    /// 用于在配置中筛选需要转发的事件类型，目前仅实现 Push，
    /// 预留其他事件类型以便后续扩展。
    /// </remarks>
    public static class GitLabEventType
    {
        /// <summary>代码推送事件</summary>
        public const string Push = "Push";

        /// <summary>合并请求事件</summary>
        public const string MergeRequest = "MergeRequest";

        /// <summary>流水线事件</summary>
        public const string Pipeline = "Pipeline";

        /// <summary>标签事件</summary>
        public const string TagPush = "TagPush";

        /// <summary>Issue 事件</summary>
        public const string Issue = "Issue";

        /// <summary>评论事件</summary>
        public const string Note = "Note";
    }
}