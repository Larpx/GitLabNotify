using System.Text.Json;
using GitLabNotify.Models;
using GitLabNotify.Services;

namespace GitLabNotify.Formatters;

/// <summary>
/// 钉钉群机器人消息格式化器
/// </summary>
/// <remarks>
/// 钉钉群机器人 API 文档：
/// https://open.dingtalk.com/document/robots/custom-robot-access
/// 
/// 支持的 msgtype：text、markdown。
/// 本实现使用 markdown 类型。
/// </remarks>
public class DingTalkFormatter : IEventFormatter
{
    /// <inheritdoc/>
    public string TargetType => Models.TargetType.DingTalk;

    /// <inheritdoc/>
    public string Format(string eventType, string payload)
    {
        try
        {
            var eventObj = JsonSerializer.Deserialize<GitLabPushEvent>(payload);
            if (eventObj == null)
            {
                return FormatError("解析 GitLab Push 事件失败");
            }

            return eventType switch
            {
                GitLabEventType.Push => FormatPushEvent(eventObj),
                _ => FormatUnsupported(eventType)
            };
        }
        catch (Exception ex)
        {
            return FormatError($"格式化异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 格式化 Push 事件
    /// </summary>
    private static string FormatPushEvent(GitLabPushEvent evt)
    {
        var branch = evt.Ref?.Replace("refs/heads/", string.Empty) ?? "unknown";
        var project = evt.Project?.PathWithNamespace ?? evt.Project?.Name ?? "unknown";
        var projectUrl = evt.Project?.WebUrl ?? evt.Project?.Homepage ?? string.Empty;
        var userName = evt.UserName ?? "unknown";
        var commitCount = evt.TotalCommitsCount;

        var lines = new List<string>
        {
            "### GitLab 推送通知",
            "",
            $"**项目**: [{project}]({projectUrl})",
            $"**分支**: {branch}",
            $"**推送人**: {userName}",
            $"**提交数**: {commitCount}"
        };

        if (evt.Commits.Count > 0)
        {
            lines.Add("");
            lines.Add("**提交记录**:");
            var showCount = Math.Min(5, evt.Commits.Count);
            for (var i = 0; i < showCount; i++)
            {
                var commit = evt.Commits[i];
                var shortSha = commit.Id?.Length >= 8 ? commit.Id[..8] : commit.Id;
                var msg = (commit.Message ?? string.Empty).Trim();
                var firstLine = msg.Split('\n')[0];
                if (firstLine.Length > 80)
                {
                    firstLine = firstLine[..77] + "...";
                }

                var commitUrl = commit.Url ?? string.Empty;
                if (!string.IsNullOrEmpty(commitUrl))
                {
                    lines.Add($"- [{shortSha}]({commitUrl}) {firstLine}");
                }
                else
                {
                    lines.Add($"- {shortSha} {firstLine}");
                }
            }

            if (evt.Commits.Count > 5)
            {
                lines.Add($"- ...还有 {evt.Commits.Count - 5} 个提交");
            }
        }

        var title = $"GitLab 推送 - {project}";
        var text = string.Join("\n", lines);

        var message = new
        {
            msgtype = "markdown",
            markdown = new { title, text }
        };

        return JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string FormatError(string error)
    {
        var message = new
        {
            msgtype = "text",
            text = new { content = $"GitLabNotify 错误: {error}" }
        };
        return JsonSerializer.Serialize(message);
    }

    private static string FormatUnsupported(string eventType)
    {
        var message = new
        {
            msgtype = "text",
            text = new { content = $"GitLabNotify 收到暂不支持的事件类型: {eventType}" }
        };
        return JsonSerializer.Serialize(message);
    }
}
