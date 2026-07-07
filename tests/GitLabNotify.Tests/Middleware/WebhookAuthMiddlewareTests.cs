using FluentAssertions;
using GitLabNotify.Middleware;
using GitLabNotify.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;

namespace GitLabNotify.Tests.Middleware;

/// <summary>
/// WebhookAuthMiddleware 鉴权中间件测试
/// </summary>
public class WebhookAuthMiddlewareTests
{
    private readonly List<string> _secrets = new() { "secret-abc", "secret-xyz" };

    [Fact]
    public async Task InvokeAsync_OnNonWebhookPath_ShouldPassThrough()
    {
        // Arrange
        var context = CreateContext("/health", "GET");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptySecrets_ShouldSkipAuth()
    {
        // Arrange
        var context = CreateContext("/webhook", "POST");
        var calledNext = false;
        var middleware = CreateMiddleware(_ => { calledNext = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = new List<string>() }));

        // Assert
        calledNext.Should().BeTrue("空 Secret 列表应跳过鉴权");
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WithNullSecrets_ShouldSkipAuth()
    {
        // Arrange
        var context = CreateContext("/webhook", "POST");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = null! }));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_MissingTokenHeader_ShouldReturn401()
    {
        // Arrange
        var context = CreateContext("/webhook", "POST");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_EmptyTokenHeader_ShouldReturn401()
    {
        // Arrange
        var context = CreateContext("/webhook", "POST");
        context.Request.Headers["X-Gitlab-Token"] = "";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_ShouldReturn401()
    {
        // Arrange
        var context = CreateContext("/webhook", "POST");
        context.Request.Headers["X-Gitlab-Token"] = "wrong-secret";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_ShouldCallNext()
    {
        // Arrange
        var context = CreateContext("/webhook", "POST");
        context.Request.Headers["X-Gitlab-Token"] = "secret-abc";
        var calledNext = false;
        var middleware = CreateMiddleware(_ => { calledNext = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        calledNext.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_ValidSecondToken_ShouldCallNext()
    {
        // Arrange - 验证多 Secret 支持
        var context = CreateContext("/webhook", "POST");
        context.Request.Headers["X-Gitlab-Token"] = "secret-xyz"; // 第二个 Secret
        var calledNext = false;
        var middleware = CreateMiddleware(_ => { calledNext = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        calledNext.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WebhookSubpath_ShouldAlsoAuth()
    {
        // Arrange: /webhook 开头的任意路径都应鉴权
        var context = CreateContext("/webhook/test", "POST");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, Options.Create(new GitLabOptions { WebhookSecrets = _secrets }));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>构造测试用的 HttpContext</summary>
    private static DefaultHttpContext CreateContext(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Request.Headers.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>构造中间件实例</summary>
    private static WebhookAuthMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new WebhookAuthMiddleware(next, NullLogger<WebhookAuthMiddleware>.Instance);
    }
}
