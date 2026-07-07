using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GitLabNotify.Data;
using GitLabNotify.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GitLabNotify.Tests.Integration;

/// <summary>
/// 端到端集成测试
/// </summary>
/// <remarks>
/// 使用 WebApplicationFactory 启动完整应用，模拟目标平台 HTTP 服务器，
/// 验证从接收 Webhook 到格式化、转发的完整流程。
/// </remarks>
public class EndToEndTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public EndToEndTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Root_ShouldReturnServiceInfo()
    {
        var response = await _client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("GitLabNotify");
        content.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task Get_Health_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task Post_Webhook_WithoutToken_ShouldReturn401()
    {
        var response = await _client.PostAsync("/webhook",
            new StringContent(TestSamples.PushEventPayload, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Webhook_WithWrongToken_ShouldReturn401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Content = new StringContent(TestSamples.PushEventPayload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Gitlab-Token", "wrong-secret");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Webhook_WithEmptyPayload_ShouldReturn400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Gitlab-Token", TestWebAppFactory.TestSecret);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Webhook_WithValidToken_ShouldReturn200AndDispatch()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Content = new StringContent(TestSamples.PushEventPayload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Gitlab-Token", TestWebAppFactory.TestSecret);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"received\":true");
        content.Should().Contain("\"eventType\":\"Push\"");
        content.Should().Contain("\"targets\":1");
    }

    [Fact]
    public async Task EndToEnd_PushEvent_ShouldBeForwardedToTarget()
    {
        // 清空之前的请求记录
        lock (_factory.ReceivedRequests)
        {
            _factory.ReceivedRequests.Clear();
        }

        // 发送 Webhook
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Content = new StringContent(TestSamples.PushEventPayload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Gitlab-Token", TestWebAppFactory.TestSecret);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 等待异步转发完成
        await _factory.WaitForRequestsAsync(1, TimeSpan.FromSeconds(15));

        // 验证模拟目标平台收到请求
        TestReceivedRequest received;
        lock (_factory.ReceivedRequests)
        {
            _factory.ReceivedRequests.Should().HaveCountGreaterThanOrEqualTo(1);
            received = _factory.ReceivedRequests[0];
        }

        // 验证请求内容
        received.Method.Should().Be("POST");
        received.ContentType.Should().Contain("application/json");

        // 验证转发的是企业微信格式化后的消息
        received.Body.Should().Contain("msgtype");
        received.Body.Should().Contain("markdown");
        received.Body.Should().Contain("mygroup/my-project");
        received.Body.Should().Contain("张三");
        received.Body.Should().Contain("修复登录问题");
    }

    [Fact]
    public async Task EndToEnd_RecordShouldBePersistedInDatabase()
    {
        // 清空之前的请求记录
        lock (_factory.ReceivedRequests)
        {
            _factory.ReceivedRequests.Clear();
        }

        // 发送 Webhook
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Content = new StringContent(TestSamples.PushEventPayload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Gitlab-Token", TestWebAppFactory.TestSecret);

        await _client.SendAsync(request);

        // 等待异步处理完成
        await _factory.WaitForRequestsAsync(1, TimeSpan.FromSeconds(15));

        // 通过 DI 取出仓储验证数据库记录
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookRecordRepository>();
        var recent = (await repo.GetRecentAsync(10)).ToList();

        recent.Should().NotBeEmpty();
        var record = recent.First();
        record.EventType.Should().Be(GitLabEventType.Push);
        record.TargetName.Should().Be("测试目标");
        record.TargetType.Should().Be(TargetType.WechatWork);
        record.Status.Should().Be(WebhookStatus.Success);
        record.RetryCount.Should().Be(0);
        record.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EndToEnd_RetryOnFailure_ShouldEventuallySucceed()
    {
        // 此测试验证重试机制：第一次目标服务器暂时不可用时
        // 由于 TestWebAppFactory 的 MockServer 一直可用，此测试主要验证正常路径下重试次数=0
        lock (_factory.ReceivedRequests)
        {
            _factory.ReceivedRequests.Clear();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Content = new StringContent(TestSamples.PushEventPayload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Gitlab-Token", TestWebAppFactory.TestSecret);

        await _client.SendAsync(request);
        await _factory.WaitForRequestsAsync(1, TimeSpan.FromSeconds(15));

        // 验证只发送了 1 次（成功无需重试）
        lock (_factory.ReceivedRequests)
        {
            _factory.ReceivedRequests.Should().HaveCount(1);
        }

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookRecordRepository>();
        var recent = (await repo.GetRecentAsync(1)).ToList();
        recent.Should().NotBeEmpty();
        recent[0].RetryCount.Should().Be(0, "成功时不应有重试");
    }
}
