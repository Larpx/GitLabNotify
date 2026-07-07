using FluentAssertions;
using GitLabNotify.Data;
using GitLabNotify.Models;
using GitLabNotify.Services;
using GitLabNotify.Targets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GitLabNotify.Tests.Services;

/// <summary>
/// WebhookProcessor 处理器测试（重试逻辑、状态流转）
/// </summary>
public class WebhookProcessorTests
{
    private readonly Mock<IWebhookRecordRepository> _repoMock = new();
    private readonly Mock<IWebhookTarget> _targetMock = new();
    private readonly PipelineOptions _options = new()
    {
        MaxRetryCount = 3,
        InitialRetryDelaySeconds = 0, // 测试用 0 秒以加速
        ChannelCapacity = 100
    };

    private WebhookProcessor CreateProcessor()
    {
        var formatters = new List<IEventFormatter>();
        var targets = new List<IWebhookTarget> { _targetMock.Object };
        var optionsWrapper = Options.Create(_options);
        return new WebhookProcessor(
            formatters,
            targets,
            _repoMock.Object,
            optionsWrapper,
            NullLogger<WebhookProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_WhenSendSucceeds_ShouldMarkAsSuccess()
    {
        // Arrange
        _targetMock.Setup(t => t.TargetType).Returns(TargetType.WechatWork);
        _targetMock.Setup(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var task = CreateTestTask();
        var processor = CreateProcessor();

        // Act
        await processor.ProcessAsync(task);

        // Assert
        // 应该被标记为 Processing → Success
        _repoMock.Verify(r => r.UpdateStatusAsync(
            task.RecordId, WebhookStatus.Processing, 0, null, null), Times.Once);
        _repoMock.Verify(r => r.UpdateStatusAsync(
            task.RecordId, WebhookStatus.Success, 0, null, It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenAllRetriesFail_ShouldMarkAsFailed()
    {
        // Arrange
        _targetMock.Setup(t => t.TargetType).Returns(TargetType.WechatWork);
        _targetMock.Setup(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // 永远失败

        var task = CreateTestTask();
        var processor = CreateProcessor();

        // Act
        await processor.ProcessAsync(task);

        // Assert
        // MaxRetryCount=3，所以总共尝试 4 次（初始 1 次 + 重试 3 次）
        _targetMock.Verify(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        // 最终状态为 Failed
        _repoMock.Verify(r => r.UpdateStatusAsync(
            task.RecordId, WebhookStatus.Failed, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenTargetThrows_ShouldRetryAndMarkFailed()
    {
        // Arrange
        _targetMock.Setup(t => t.TargetType).Returns(TargetType.WechatWork);
        _targetMock.Setup(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("连接失败"));

        var task = CreateTestTask();
        var processor = CreateProcessor();

        // Act
        await processor.ProcessAsync(task);

        // Assert
        _targetMock.Verify(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        _repoMock.Verify(r => r.UpdateStatusAsync(
            task.RecordId, WebhookStatus.Failed, It.IsAny<int>(), "连接失败", It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenSucceedsOnSecondAttempt_ShouldMarkAsSuccess()
    {
        // Arrange: 第一次失败，第二次成功
        _targetMock.Setup(t => t.TargetType).Returns(TargetType.WechatWork);
        var callCount = 0;
        _targetMock.Setup(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount >= 2; // 第一次返回 false，第二次返回 true
            });

        var task = CreateTestTask();
        var processor = CreateProcessor();

        // Act
        await processor.ProcessAsync(task);

        // Assert
        _targetMock.Verify(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _repoMock.Verify(r => r.UpdateStatusAsync(
            task.RecordId, WebhookStatus.Success, 1, null, It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenTargetNotFound_ShouldMarkAsFailed()
    {
        // Arrange: 不注册任何匹配的 target
        _targetMock.Setup(t => t.TargetType).Returns(TargetType.WechatWork);
        var processor = new WebhookProcessor(
            new List<IEventFormatter>(),
            new List<IWebhookTarget>(), // 空 targets
            _repoMock.Object,
            Options.Create(_options),
            NullLogger<WebhookProcessor>.Instance);

        // 构造一个不匹配任何 target 的任务
        var task = new WebhookTask
        {
            RecordId = 1,
            EventType = GitLabEventType.Push,
            Payload = "{}",
            Target = new TargetOptions { Name = "无匹配", Type = "UnknownType", Url = "http://x" }
        };

        // Act
        await processor.ProcessAsync(task);

        // Assert
        _repoMock.Verify(r => r.UpdateStatusAsync(
            task.RecordId, WebhookStatus.Failed, 0, It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Once);
    }

    private static WebhookTask CreateTestTask()
    {
        return new WebhookTask
        {
            RecordId = 1,
            EventType = GitLabEventType.Push,
            Payload = TestSamples.PushEventPayload,
            Target = new TargetOptions
            {
                Name = "测试目标",
                Type = TargetType.WechatWork,
                Url = "http://test.example.com/webhook",
                Enabled = true
            }
        };
    }
}

/// <summary>
/// WebhookDispatcher 分发器测试
/// </summary>
public class WebhookDispatcherTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SqliteRecordRepository _repository;
    private readonly WebhookPipeline _pipeline;

    public WebhookDispatcherTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"gitlabnotify_disp_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
        DbInitializer.Initialize(_connectionString);
        _repository = new SqliteRecordRepository(_connectionString, NullLogger<SqliteRecordRepository>.Instance);

        // 使用真实 WebhookPipeline（不调用 StartAsync，因此后台任务不会真正执行）
        // 只验证 EnqueueAsync 是否被调用，Channel 是否收到任务
        var pipelineOptions = Options.Create(new PipelineOptions { ChannelCapacity = 100 });
        var processorMock = new Mock<IWebhookProcessor>();
        _pipeline = new WebhookPipeline(
            processorMock.Object,
            pipelineOptions,
            NullLogger<WebhookPipeline>.Instance);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
        catch { /* 忽略 */ }
    }

    [Fact]
    public async Task DispatchAsync_WithMatchingTarget_ShouldEnqueueAndPersist()
    {
        // Arrange
        var targets = new List<TargetOptions>
        {
            new() { Name = "目标A", Type = TargetType.WechatWork, Url = "http://a", Enabled = true, Events = new() { GitLabEventType.Push } }
        };
        var dispatcher = new WebhookDispatcher(
            _pipeline,
            _repository,
            Options.Create(targets),
            NullLogger<WebhookDispatcher>.Instance);

        // Act
        var count = await dispatcher.DispatchAsync(GitLabEventType.Push, TestSamples.PushEventPayload);

        // Assert
        count.Should().Be(1);
        _pipeline.PendingCount.Should().Be(1, "应有 1 个任务在队列中");

        // 验证数据库中有记录
        var recent = (await _repository.GetRecentAsync()).ToList();
        recent.Should().HaveCount(1);
        recent[0].EventType.Should().Be(GitLabEventType.Push);
        recent[0].TargetName.Should().Be("目标A");
        recent[0].Status.Should().Be(WebhookStatus.Pending);
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleMatchingTargets_ShouldEnqueueAll()
    {
        // Arrange
        var targets = new List<TargetOptions>
        {
            new() { Name = "企业微信", Type = TargetType.WechatWork, Url = "http://a", Enabled = true, Events = new() { GitLabEventType.Push } },
            new() { Name = "钉钉", Type = TargetType.DingTalk, Url = "http://b", Enabled = true, Events = new() { GitLabEventType.Push } },
            new() { Name = "飞书", Type = TargetType.Feishu, Url = "http://c", Enabled = true, Events = new() { GitLabEventType.Push } }
        };
        var dispatcher = new WebhookDispatcher(
            _pipeline,
            _repository,
            Options.Create(targets),
            NullLogger<WebhookDispatcher>.Instance);

        // Act
        var count = await dispatcher.DispatchAsync(GitLabEventType.Push, TestSamples.PushEventPayload);

        // Assert
        count.Should().Be(3);
        _pipeline.PendingCount.Should().Be(3, "应有 3 个任务在队列中");

        var recent = (await _repository.GetRecentAsync()).ToList();
        recent.Should().HaveCount(3);
    }

    [Fact]
    public async Task DispatchAsync_WithNoMatchingTarget_ShouldReturnZeroAndNotEnqueue()
    {
        // Arrange
        var targets = new List<TargetOptions>
        {
            new() { Name = "只订阅MR", Type = TargetType.WechatWork, Url = "http://a", Enabled = true, Events = new() { GitLabEventType.MergeRequest } }
        };
        var dispatcher = new WebhookDispatcher(
            _pipeline,
            _repository,
            Options.Create(targets),
            NullLogger<WebhookDispatcher>.Instance);

        // Act
        var count = await dispatcher.DispatchAsync(GitLabEventType.Push, TestSamples.PushEventPayload);

        // Assert
        count.Should().Be(0);
        _pipeline.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task DispatchAsync_WithDisabledTarget_ShouldSkip()
    {
        // Arrange
        var targets = new List<TargetOptions>
        {
            new() { Name = "已禁用", Type = TargetType.WechatWork, Url = "http://a", Enabled = false, Events = new() { GitLabEventType.Push } }
        };
        var dispatcher = new WebhookDispatcher(
            _pipeline,
            _repository,
            Options.Create(targets),
            NullLogger<WebhookDispatcher>.Instance);

        // Act
        var count = await dispatcher.DispatchAsync(GitLabEventType.Push, TestSamples.PushEventPayload);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task DispatchAsync_WithEmptyEventsList_ShouldSubscribeAll()
    {
        // Arrange: Events=null 表示订阅所有
        var targets = new List<TargetOptions>
        {
            new() { Name = "全部订阅", Type = TargetType.WechatWork, Url = "http://a", Enabled = true, Events = null }
        };
        var dispatcher = new WebhookDispatcher(
            _pipeline,
            _repository,
            Options.Create(targets),
            NullLogger<WebhookDispatcher>.Instance);

        // Act
        var count = await dispatcher.DispatchAsync(GitLabEventType.Push, TestSamples.PushEventPayload);

        // Assert
        count.Should().Be(1);
    }
}
