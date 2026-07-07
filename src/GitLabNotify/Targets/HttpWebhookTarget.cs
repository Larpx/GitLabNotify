using System.Net.Http.Headers;
using System.Text;
using GitLabNotify.Services;

namespace GitLabNotify.Targets;

/// <summary>
/// 通用 HTTP Webhook 目标适配器
/// </summary>
/// <remarks>
/// 将格式化后的消息体以 JSON POST 方式发送到目标 URL。
/// 这是所有其他目标适配器的基础，企业微信/钉钉/飞书都继承此类。
/// </remarks>
public class HttpWebhookTarget : IWebhookTarget
{
    /// <inheritdoc/>
    public virtual string TargetType => Models.TargetType.Http;

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWebhookTarget> _logger;

    public HttpWebhookTarget(HttpClient httpClient, ILogger<HttpWebhookTarget> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<bool> SendAsync(string url, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogError("目标 URL 为空");
            return false;
        }

        using var content = new StringContent(message, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("HTTP Webhook 发送成功，URL={Url}, 状态码={StatusCode}, 响应={Response}",
                    url, (int)response.StatusCode, Truncate(responseBody, 200));
                return true;
            }

            _logger.LogWarning("HTTP Webhook 发送失败，URL={Url}, 状态码={StatusCode}, 响应={Response}",
                url, (int)response.StatusCode, Truncate(responseBody, 200));
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP Webhook 请求异常，URL={Url}", url);
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "HTTP Webhook 请求超时，URL={Url}", url);
            return false;
        }
    }

    /// <summary>截断字符串到指定长度</summary>
    protected static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}

/// <summary>
/// 企业微信群机器人目标适配器
/// </summary>
/// <remarks>
/// 企业微信群机器人接收 JSON 格式消息，直接 POST 即可。
/// </remarks>
public class WechatWorkTarget : HttpWebhookTarget
{
    /// <inheritdoc/>
    public override string TargetType => Models.TargetType.WechatWork;

    public WechatWorkTarget(HttpClient httpClient, ILogger<HttpWebhookTarget> logger)
        : base(httpClient, logger)
    {
    }
}

/// <summary>
/// 钉钉群机器人目标适配器
/// </summary>
/// <remarks>
/// 钉钉群机器人接收 JSON 格式消息，直接 POST 即可。
/// 若配置了加签密钥，需要额外计算签名并拼接到 URL。本实现暂不支持加签，仅支持基础模式。
/// </remarks>
public class DingTalkTarget : HttpWebhookTarget
{
    /// <inheritdoc/>
    public override string TargetType => Models.TargetType.DingTalk;

    public DingTalkTarget(HttpClient httpClient, ILogger<HttpWebhookTarget> logger)
        : base(httpClient, logger)
    {
    }
}

/// <summary>
/// 飞书群机器人目标适配器
/// </summary>
/// <remarks>
/// 飞书自定义机器人接收 JSON 格式消息，直接 POST 即可。
/// 若配置了签名校验，需要额外计算签名。本实现暂不支持签名，仅支持基础模式。
/// </remarks>
public class FeishuTarget : HttpWebhookTarget
{
    /// <inheritdoc/>
    public override string TargetType => Models.TargetType.Feishu;

    public FeishuTarget(HttpClient httpClient, ILogger<HttpWebhookTarget> logger)
        : base(httpClient, logger)
    {
    }
}
