using System.Text.Json;

namespace Larpx.PersonalTools.GitLabNotify.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// </summary>
    /// <remarks>
    /// 部署在管道最外层,兜住下游所有未处理异常。
    /// 记录结构化错误日志,响应未开始写入时返回 500 + 极简 JSON,绝不向客户端暴露 ex.Message。
    /// 避免 GitLab 端看到非 200 响应时重试风暴,同时防止内部异常详情泄漏。
    /// </remarks>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // 记录完整异常到日志(含堆栈),响应体仅返回固定文案
                _logger.LogError(ex,
                    "处理请求时发生未预期异常，路径={Path}，方法={Method}，来自 {RemoteIp}",
                    context.Request.Path,
                    context.Request.Method,
                    context.Connection.RemoteIpAddress);

                // 响应已开始写入则不能安全重置,直接放弃,让客户端看到不完整响应
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("响应已开始写入,无法追加统一错误响应");
                    return;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";

                var body = JsonSerializer.Serialize(new { error = "Internal Server Error" });
                await context.Response.WriteAsync(body);
            }
        }
    }
}