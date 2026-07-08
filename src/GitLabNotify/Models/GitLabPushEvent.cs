using System.Text.Json.Serialization;

namespace Larpx.PersonalTools.GitLabNotify.Models
{
    /// <summary>
    /// GitLab Push 事件模型
    /// </summary>
    /// <remarks>
    /// 仅保留转发消息所需的字段，避免对完整 GitLab Webhook Payload 建模。
    /// 完整字段定义参考 GitLab 官方文档：
    /// https://docs.gitlab.com/ee/user/project/integrations/webhook_events.html#push-events
    /// </remarks>
    public class GitLabPushEvent
    {
        /// <summary>事件类型标识（固定为 "push"）</summary>
        [JsonPropertyName("object_kind")]
        public string ObjectKind { get; set; } = "push";

        /// <summary>Before 推送的提交 SHA</summary>
        [JsonPropertyName("before")]
        public string? Before { get; set; }

        /// <summary>After 推送的提交 SHA</summary>
        [JsonPropertyName("after")]
        public string? After { get; set; }

        /// <summary>推送的目标分支引用（如 refs/heads/main）</summary>
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        /// <summary>检出分支名（如 main）</summary>
        [JsonPropertyName("checkout_sha")]
        public string? CheckoutSha { get; set; }

        /// <summary>用户 ID</summary>
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        /// <summary>用户名</summary>
        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }

        /// <summary>用户邮箱</summary>
        [JsonPropertyName("user_email")]
        public string? UserEmail { get; set; }

        /// <summary>用户头像 URL</summary>
        [JsonPropertyName("user_avatar")]
        public string? UserAvatar { get; set; }

        /// <summary>项目 ID</summary>
        [JsonPropertyName("project_id")]
        public int ProjectId { get; set; }

        /// <summary>项目信息</summary>
        [JsonPropertyName("project")]
        public GitLabProject Project { get; set; } = new();

        /// <summary>仓库信息</summary>
        [JsonPropertyName("repository")]
        public GitLabRepository? Repository { get; set; }

        /// <summary>提交列表</summary>
        [JsonPropertyName("commits")]
        public List<GitLabCommit> Commits { get; set; } = new();

        /// <summary>提交总数</summary>
        [JsonPropertyName("total_commits_count")]
        public int TotalCommitsCount { get; set; }
    }

    /// <summary>
    /// GitLab 项目信息
    /// </summary>
    public class GitLabProject
    {
        /// <summary>项目名称</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>项目完整描述</summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>项目 Web URL</summary>
        [JsonPropertyName("web_url")]
        public string? WebUrl { get; set; }

        /// <summary>项目头像 URL</summary>
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        /// <summary>项目命名空间路径</summary>
        [JsonPropertyName("namespace")]
        public string? Namespace { get; set; }

        /// <summary>项目访问级别</summary>
        [JsonPropertyName("visibility_level")]
        public int VisibilityLevel { get; set; }

        /// <summary>项目路径（namespace/name）</summary>
        [JsonPropertyName("path_with_namespace")]
        public string? PathWithNamespace { get; set; }

        /// <summary>默认分支</summary>
        [JsonPropertyName("default_branch")]
        public string? DefaultBranch { get; set; }

        /// <summary>项目主页 URL</summary>
        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        /// <summary>项目 HTTP 克隆 URL</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>项目 SSH 克隆 URL</summary>
        [JsonPropertyName("ssh_url")]
        public string? SshUrl { get; set; }

        /// <summary>项目 HTTP 克隆 URL</summary>
        [JsonPropertyName("http_url")]
        public string? HttpUrl { get; set; }
    }

    /// <summary>
    /// GitLab 仓库信息（旧版字段，仅用于兼容）
    /// </summary>
    public class GitLabRepository
    {
        /// <summary>仓库名称</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>仓库 URL</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>仓库描述</summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>仓库主页</summary>
        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }
    }

    /// <summary>
    /// GitLab 提交信息
    /// </summary>
    public class GitLabCommit
    {
        /// <summary>提交 ID（SHA）</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>提交消息</summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>提交时间戳</summary>
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        /// <summary>提交 URL</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>提交作者</summary>
        [JsonPropertyName("author")]
        public GitLabCommitAuthor Author { get; set; } = new();

        /// <summary>新增行数</summary>
        [JsonPropertyName("added")]
        public List<string> Added { get; set; } = new();

        /// <summary>修改行数</summary>
        [JsonPropertyName("modified")]
        public List<string> Modified { get; set; } = new();

        /// <summary>删除行数</summary>
        [JsonPropertyName("removed")]
        public List<string> Removed { get; set; } = new();
    }

    /// <summary>
    /// GitLab 提交作者信息
    /// </summary>
    public class GitLabCommitAuthor
    {
        /// <summary>作者名称</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>作者邮箱</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
