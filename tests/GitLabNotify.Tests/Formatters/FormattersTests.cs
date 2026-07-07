using System.Text.Json;
using FluentAssertions;
using GitLabNotify.Formatters;
using GitLabNotify.Models;

namespace GitLabNotify.Tests.Formatters;

/// <summary>
/// 企业微信格式化器测试
/// </summary>
public class WechatWorkFormatterTests
{
    private readonly WechatWorkFormatter _formatter = new();

    [Fact]
    public void TargetType_ShouldBeWechatWork()
    {
        _formatter.TargetType.Should().Be(TargetType.WechatWork);
    }

    [Fact]
    public void Format_PushEvent_ShouldReturnMarkdownMessage()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);

        result.Should().NotBeNullOrWhiteSpace();

        // 解析为 JSON 验证结构
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msgtype").GetString().Should().Be("markdown");
        var content = doc.RootElement.GetProperty("markdown").GetProperty("content").GetString();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Format_PushEvent_ShouldContainProjectInfo()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var content = JsonDocument.Parse(result).RootElement.GetProperty("markdown").GetProperty("content").GetString();

        content.Should().Contain("mygroup/my-project");
        content.Should().Contain("main");
        content.Should().Contain("张三");
        content.Should().Contain("2"); // 提交数
    }

    [Fact]
    public void Format_PushEvent_ShouldContainCommitInfo()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var content = JsonDocument.Parse(result).RootElement.GetProperty("markdown").GetProperty("content").GetString();

        // 应包含第一条提交
        content.Should().Contain("修复登录问题");
        content.Should().Contain("f6e5d4c3"); // 8位短 SHA
    }

    [Fact]
    public void Format_PushEvent_ShouldContainCommitUrl()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var content = JsonDocument.Parse(result).RootElement.GetProperty("markdown").GetProperty("content").GetString();

        content.Should().Contain("https://gitlab.example.com/mygroup/my-project/-/commit/f6e5d4c3b2a1");
    }

    [Fact]
    public void Format_PushEvent_ShouldContainSecondCommit()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var content = JsonDocument.Parse(result).RootElement.GetProperty("markdown").GetProperty("content").GetString();

        content.Should().Contain("新增用户管理模块");
        content.Should().Contain("a1b2c3d4");
    }

    [Fact]
    public void Format_EmptyPush_ShouldNotContainCommitList()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.EmptyPushPayload);
        var content = JsonDocument.Parse(result).RootElement.GetProperty("markdown").GetProperty("content").GetString();

        content.Should().Contain("推送通知");
        content.Should().Contain("0"); // 提交数 0
    }

    [Fact]
    public void Format_InvalidJson_ShouldReturnTextErrorMessage()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.InvalidJson);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msgtype").GetString().Should().Be("text");
        var content = doc.RootElement.GetProperty("text").GetProperty("content").GetString();
        content.Should().Contain("错误");
    }

    [Fact]
    public void Format_UnsupportedEvent_ShouldReturnTextMessage()
    {
        var result = _formatter.Format(GitLabEventType.MergeRequest, TestSamples.PushEventPayload);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msgtype").GetString().Should().Be("text");
        var content = doc.RootElement.GetProperty("text").GetProperty("content").GetString();
        content.Should().Contain("暂不支持");
    }

    [Fact]
    public void Format_PushEvent_ChineseShouldNotBeEscaped()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);

        // 中文不应被转义为 \uXXXX
        result.Should().Contain("张三");
        result.Should().NotContain("\\u5f20\\u4e09");
    }
}

/// <summary>
/// 钉钉格式化器测试
/// </summary>
public class DingTalkFormatterTests
{
    private readonly DingTalkFormatter _formatter = new();

    [Fact]
    public void TargetType_ShouldBeDingTalk()
    {
        _formatter.TargetType.Should().Be(TargetType.DingTalk);
    }

    [Fact]
    public void Format_PushEvent_ShouldReturnMarkdownWithTitle()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msgtype").GetString().Should().Be("markdown");
        var markdown = doc.RootElement.GetProperty("markdown");

        markdown.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        markdown.GetProperty("text").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Format_PushEvent_ShouldContainKeyInfo()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var text = JsonDocument.Parse(result).RootElement.GetProperty("markdown").GetProperty("text").GetString();

        text.Should().Contain("mygroup/my-project");
        text.Should().Contain("main");
        text.Should().Contain("张三");
        text.Should().Contain("修复登录问题");
    }

    [Fact]
    public void Format_InvalidJson_ShouldReturnTextError()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.InvalidJson);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msgtype").GetString().Should().Be("text");
    }
}

/// <summary>
/// 飞书格式化器测试
/// </summary>
public class FeishuFormatterTests
{
    private readonly FeishuFormatter _formatter = new();

    [Fact]
    public void TargetType_ShouldBeFeishu()
    {
        _formatter.TargetType.Should().Be(TargetType.Feishu);
    }

    [Fact]
    public void Format_PushEvent_ShouldReturnInteractiveCard()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msg_type").GetString().Should().Be("interactive");

        var card = doc.RootElement.GetProperty("card");
        var header = card.GetProperty("header");
        header.GetProperty("template").GetString().Should().Be("blue");
        header.GetProperty("title").GetProperty("content").GetString().Should().NotBeNullOrEmpty();

        var elements = card.GetProperty("elements");
        elements.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Format_PushEvent_ShouldContainKeyInfo()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var doc = JsonDocument.Parse(result);
        var elements = doc.RootElement.GetProperty("card").GetProperty("elements");

        // 第一个 div 元素应包含项目信息
        var firstDivContent = elements[0].GetProperty("text").GetProperty("content").GetString();
        firstDivContent.Should().Contain("mygroup/my-project");
        firstDivContent.Should().Contain("main");
        firstDivContent.Should().Contain("张三");
    }

    [Fact]
    public void Format_PushEvent_ShouldHaveCommitsElement_WhenHasCommits()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.PushEventPayload);
        var elements = JsonDocument.Parse(result).RootElement.GetProperty("card").GetProperty("elements");

        elements.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        var commitContent = elements[1].GetProperty("text").GetProperty("content").GetString();
        commitContent.Should().Contain("提交记录");
        commitContent.Should().Contain("修复登录问题");
    }

    [Fact]
    public void Format_EmptyPush_ShouldHaveSingleElement()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.EmptyPushPayload);
        var elements = JsonDocument.Parse(result).RootElement.GetProperty("card").GetProperty("elements");

        // 无提交时只有基本信息 div，没有提交记录 div
        elements.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Format_InvalidJson_ShouldReturnTextError()
    {
        var result = _formatter.Format(GitLabEventType.Push, TestSamples.InvalidJson);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("msg_type").GetString().Should().Be("text");
    }
}
