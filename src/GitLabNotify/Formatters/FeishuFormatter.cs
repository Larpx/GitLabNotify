using Larpx.PersonalTools.GitLabNotify.Models;
using Larpx.PersonalTools.GitLabNotify.Services;
using System.Text.Json;

namespace Larpx.PersonalTools.GitLabNotify.Formatters
{
    /// <summary>
    /// 飞书群机器人消息格式化器
    /// </summary>
    /// <remarks>
    /// 飞书自定义机器人 API 文档：
    /// https://open.feishu.cn/document/client-docs/bot-v3/add-custom-bot
    /// 
    /// 使用交互式卡片消息（interactive）格式。
    /// </remarks>
    public class FeishuFormatter : IEventFormatter
    {
        /// <inheritdoc/>
        public string TargetType => Models.TargetType.Feishu;

        /// <inheritdoc/>
        public string? Format(string eventType, string payload)
        {
            try
            {
                var eventObj = JsonSerializer.Deserialize<GitLabPushEvent>(payload);
                if (eventObj == null)
                {
                    return null;
                }

                return eventType switch
                {
                    GitLabEventType.Push => FormatPushEvent(eventObj),
                    _ => null
                };
            }
            catch (Exception)
            {
                return null;
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

            // 构造飞书卡片内容
            var elements = new List<object>
        {
            new
            {
                tag = "div",
                text = new
                {
                    tag = "lark_md",
                    content = $"**GitLab 推送通知**\n\n" +
                              $"**项目**: [{project}]({projectUrl})\n" +
                              $"**分支**: {branch}\n" +
                              $"**推送人**: {userName}\n" +
                              $"**提交数**: {commitCount}"
                }
            }
        };

            // 添加提交记录
            if (evt.Commits.Count > 0)
            {
                var commitLines = new List<string>();
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
                        commitLines.Add($"- [{shortSha}]({commitUrl}) {firstLine}");
                    }
                    else
                    {
                        commitLines.Add($"- {shortSha} {firstLine}");
                    }
                }

                if (evt.Commits.Count > 5)
                {
                    commitLines.Add($"- ...还有 {evt.Commits.Count - 5} 个提交");
                }

                elements.Add(new
                {
                    tag = "div",
                    text = new
                    {
                        tag = "lark_md",
                        content = "**提交记录**:\n" + string.Join("\n", commitLines)
                    }
                });
            }

            // 飞书卡片消息体
            var message = new
            {
                msg_type = "interactive",
                card = new
                {
                    header = new
                    {
                        template = "blue",
                        title = new
                        {
                            tag = "plain_text",
                            content = "GitLab 推送通知"
                        }
                    },
                    elements
                }
            };

            return JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
    }
}
