using System.Diagnostics;
using System.Security.Claims;

namespace Community_C.Utility.Logs;

public sealed class ServerRequestLoggingMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";

    private const int MaximumCorrelationIdLength = 64;
    private const string AnonymousUserId = "anonymous";

    private readonly RequestDelegate _next;
    private readonly ILogger<ServerRequestLoggingMiddleware> _logger;

    /// <summary>
    /// 다음 요청 처리기와 서버 요청 로거를 사용해 미들웨어를 초기화한다.
    /// </summary>
    public ServerRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<ServerRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// HTTP 요청을 실행하고 식별자·상태 코드·처리 시간·예외를 기록한다.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context);
        string userId = ResolveUserId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        string requestMethod = context.Request.Method;
        string requestPath = context.Request.Path.Value ?? "/";
        long startedAt = Stopwatch.GetTimestamp();
        bool clientCanceled = false;

        using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["UserId"] = userId
        });

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            clientCanceled = true;
            throw;
        }
        catch (Exception exception)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }

            ServerLog.UnhandledRequestException(
                _logger,
                exception,
                requestMethod,
                requestPath,
                correlationId,
                userId);
            throw;
        }
        finally
        {
            double elapsedMilliseconds = Math.Round(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                2);

            if (clientCanceled)
            {
                ServerLog.RequestCanceled(
                    _logger,
                    requestMethod,
                    requestPath,
                    elapsedMilliseconds,
                    correlationId,
                    userId);
            }
            else
            {
                WriteCompletionLog(
                    requestMethod,
                    requestPath,
                    context.Response.StatusCode,
                    elapsedMilliseconds,
                    correlationId,
                    userId);
            }
        }
    }

    /// <summary>
    /// HTTP 응답 상태 코드에 맞는 완료 로그 이벤트를 호출한다.
    /// </summary>
    private void WriteCompletionLog(
        string requestMethod,
        string requestPath,
        int statusCode,
        double elapsedMilliseconds,
        string correlationId,
        string userId)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            ServerLog.RequestFailed(
                _logger,
                requestMethod,
                requestPath,
                statusCode,
                elapsedMilliseconds,
                correlationId,
                userId);
            return;
        }

        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            ServerLog.RequestRejected(
                _logger,
                requestMethod,
                requestPath,
                statusCode,
                elapsedMilliseconds,
                correlationId,
                userId);
            return;
        }

        ServerLog.RequestCompleted(
            _logger,
            requestMethod,
            requestPath,
            statusCode,
            elapsedMilliseconds,
            correlationId,
            userId);
    }

    /// <summary>
    /// 요청 헤더의 Correlation ID를 검증하거나 새로운 식별자를 반환한다.
    /// </summary>
    private static string ResolveCorrelationId(HttpContext context)
    {
        string? requestedCorrelationId = context.Request.Headers[CorrelationIdHeader]
            .FirstOrDefault();

        if (IsValidCorrelationId(requestedCorrelationId))
        {
            return requestedCorrelationId!;
        }

        return string.IsNullOrWhiteSpace(context.TraceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : context.TraceIdentifier;
    }

    /// <summary>
    /// Correlation ID가 허용된 길이와 문자로 구성되었는지 확인한다.
    /// </summary>
    private static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumCorrelationIdLength)
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or
            >= '0' and <= '9' or
                '-' or '_' or '.' or ':');
    }

    /// <summary>
    /// 인증된 사용자 claim에서 내부 사용자 ID를 조회한다.
    /// </summary>
    private static string ResolveUserId(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return AnonymousUserId;
        }

        return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? AnonymousUserId;
    }
}
