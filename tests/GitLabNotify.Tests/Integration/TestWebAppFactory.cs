using System.Net;
using System.Net.Sockets;
using System.Text;
using GitLabNotify.Data;
using GitLabNotify.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GitLabNotify.Tests.Integration;

/// <summary>
/// 集成测试用 WebApplicationFactory
/// </summary>
/// <remarks>
/// 重写配置，使用临时 SQLite 文件，避免污染开发环境数据库。
/// 同时将目标 URL 指向测试用 HTTP 服务器，便于验证完整转发流程。
/// </remarks>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>临时数据库文件路径</summary>
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"gitlabnotify_integration_{Guid.NewGuid():N}.db");

    /// <summary>测试用 HTTP 服务器收到的请求列表（线程安全）</summary>
    public List<TestReceivedRequest> ReceivedRequests { get; } = new();

    /// <summary>测试用 HTTP 服务器端口</summary>
    public int MockServerPort { get; private set; }

    /// <summary>测试用 Secret Token</summary>
    public const string TestSecret = "integration-test-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 启动一个内置 HTTP 监听器模拟目标平台
        MockServerPort = StartMockServer();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitLab:WebhookSecrets:0"] = TestSecret,
                ["Persistence:ConnectionString"] = $"Data Source={DbPath}",
                ["Targets:0:Name"] = "测试目标",
                ["Targets:0:Type"] = "WechatWork",
                ["Targets:0:Url"] = $"http://localhost:{MockServerPort}/webhook",
                ["Targets:0:Enabled"] = "true",
                ["Pipeline:MaxRetryCount"] = "2",
                ["Pipeline:InitialRetryDelaySeconds"] = "0", // 测试用 0 延迟加速
                ["Pipeline:ChannelCapacity"] = "100",
                ["Serilog:MinimumLevel:Default"] = "Warning"
            }!);
        });
    }

    /// <summary>
    /// 启动模拟目标平台的 HTTP 服务器
    /// </summary>
    private int StartMockServer()
    {
        var listener = new HttpListener();
        // 选择一个可用端口
        var port = GetAvailablePort();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        // 异步处理请求
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var body = await reader.ReadToEndAsync();

                    lock (ReceivedRequests)
                    {
                        ReceivedRequests.Add(new TestReceivedRequest
                        {
                            Url = ctx.Request.Url?.ToString() ?? "",
                            Method = ctx.Request.HttpMethod,
                            Body = body,
                            ContentType = ctx.Request.ContentType ?? "",
                            ReceivedAt = DateTime.UtcNow
                        });
                    }

                    // 返回成功响应
                    ctx.Response.StatusCode = 200;
                    var responseBytes = Encoding.UTF8.GetBytes("""{"errcode":0,"errmsg":"ok"}""");
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = responseBytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(responseBytes);
                    ctx.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // 监听器已关闭
                    break;
                }
                catch (Exception)
                {
                    // 忽略其他异常以保持监听
                }
            }
        });

        return port;
    }

    /// <summary>获取可用端口</summary>
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>等待直到收到指定数量的请求，或超时</summary>
    public async Task WaitForRequestsAsync(int count, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            lock (ReceivedRequests)
            {
                if (ReceivedRequests.Count >= count)
                {
                    return;
                }
            }
            await Task.Delay(100);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { if (File.Exists(DbPath)) File.Delete(DbPath); }
            catch { /* 忽略 */ }
        }
        base.Dispose(disposing);
    }
}

/// <summary>测试用 HTTP 服务器收到的请求</summary>
public class TestReceivedRequest
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "";
    public string Body { get; set; } = "";
    public string ContentType { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}
