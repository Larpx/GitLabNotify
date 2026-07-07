using FluentAssertions;
using GitLabNotify.Models;
using GitLabNotify.Services;

namespace GitLabNotify.Tests.Models;

/// <summary>
/// TargetOptions 配置模型测试
/// </summary>
public class TargetOptionsTests
{
    [Fact]
    public void SubscribesTo_WhenEventsIsNull_ShouldSubscribeAll()
    {
        // Arrange
        var target = new TargetOptions { Events = null };

        // Act
        var result = target.SubscribesTo(GitLabEventType.Push);

        // Assert
        result.Should().BeTrue("空订阅列表应表示订阅所有事件");
    }

    [Fact]
    public void SubscribesTo_WhenEventsIsEmpty_ShouldSubscribeAll()
    {
        var target = new TargetOptions { Events = new List<string>() };

        target.SubscribesTo(GitLabEventType.Push).Should().BeTrue();
    }

    [Fact]
    public void SubscribesTo_WhenEventsContainsType_ShouldReturnTrue()
    {
        var target = new TargetOptions
        {
            Events = new List<string> { GitLabEventType.Push, GitLabEventType.MergeRequest }
        };

        target.SubscribesTo(GitLabEventType.Push).Should().BeTrue();
    }

    [Fact]
    public void SubscribesTo_WhenEventsNotContainsType_ShouldReturnFalse()
    {
        var target = new TargetOptions
        {
            Events = new List<string> { GitLabEventType.MergeRequest }
        };

        target.SubscribesTo(GitLabEventType.Push).Should().BeFalse();
    }

    [Fact]
    public void SubscribesTo_ShouldBeCaseInsensitive()
    {
        var target = new TargetOptions
        {
            Events = new List<string> { "push" } // 小写
        };

        target.SubscribesTo("Push").Should().BeTrue("事件匹配应不区分大小写");
    }
}

/// <summary>
/// WebhookDispatcher.ParseEventType 静态方法测试
/// </summary>
public class EventTypeParserTests
{
    [Fact]
    public void ParseEventType_Push_ShouldReturnPush()
    {
        var result = WebhookDispatcher.ParseEventType(TestSamples.PushEventPayload);
        result.Should().Be(GitLabEventType.Push);
    }

    [Fact]
    public void ParseEventType_TagPush_ShouldReturnTagPush()
    {
        var payload = """{ "object_kind": "tag_push" }""";
        WebhookDispatcher.ParseEventType(payload).Should().Be(GitLabEventType.TagPush);
    }

    [Fact]
    public void ParseEventType_MergeRequest_ShouldReturnMergeRequest()
    {
        var payload = """{ "object_kind": "merge_request" }""";
        WebhookDispatcher.ParseEventType(payload).Should().Be(GitLabEventType.MergeRequest);
    }

    [Fact]
    public void ParseEventType_Pipeline_ShouldReturnPipeline()
    {
        var payload = """{ "object_kind": "pipeline" }""";
        WebhookDispatcher.ParseEventType(payload).Should().Be(GitLabEventType.Pipeline);
    }

    [Fact]
    public void ParseEventType_Issue_ShouldReturnIssue()
    {
        var payload = """{ "object_kind": "issue" }""";
        WebhookDispatcher.ParseEventType(payload).Should().Be(GitLabEventType.Issue);
    }

    [Fact]
    public void ParseEventType_Note_ShouldReturnNote()
    {
        var payload = """{ "object_kind": "note" }""";
        WebhookDispatcher.ParseEventType(payload).Should().Be(GitLabEventType.Note);
    }

    [Fact]
    public void ParseEventType_UnknownKind_ShouldReturnKindString()
    {
        var payload = """{ "object_kind": "future_event" }""";
        WebhookDispatcher.ParseEventType(payload).Should().Be("future_event");
    }

    [Fact]
    public void ParseEventType_MissingObjectKind_ShouldReturnUnknown()
    {
        WebhookDispatcher.ParseEventType(TestSamples.MissingObjectKindPayload).Should().Be("Unknown");
    }

    [Fact]
    public void ParseEventType_InvalidJson_ShouldReturnUnknown()
    {
        WebhookDispatcher.ParseEventType(TestSamples.InvalidJson).Should().Be("Unknown");
    }
}
