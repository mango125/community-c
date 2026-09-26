using Community_C.Utility.Logs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Community_C.Tests;

public class ServerRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsValidatedCorrelationIdWithoutLoggingQueryString()
    {
        var logger = new TestLogger<ServerRequestLoggingMiddleware>();
        var middleware = new ServerRequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            logger);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/User/Login";
        context.Request.QueryString = new QueryString("?access_token=must-not-be-logged");
        context.Request.Headers[ServerRequestLoggingMiddleware.CorrelationIdHeader] = "request-123";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            "TestAuthentication"));

        await middleware.InvokeAsync(context);

        Assert.Equal("request-123", context.TraceIdentifier);
        Assert.Equal(
            "request-123",
            context.Response.Headers[ServerRequestLoggingMiddleware.CorrelationIdHeader].ToString());
        string log = Assert.Single(logger.Messages);
        Assert.Contains("GET /User/Login", log);
        Assert.Contains("204", log);
        Assert.Contains("user-1", log);
        Assert.DoesNotContain("must-not-be-logged", log);
    }

    [Fact]
    public async Task InvokeAsync_ReplacesInvalidCorrelationIdAndLogsUnhandledException()
    {
        var logger = new TestLogger<ServerRequestLoggingMiddleware>();
        var middleware = new ServerRequestLoggingMiddleware(
            _ => throw new InvalidOperationException("test failure"),
            logger);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/Board/WriteBoard";
        context.Request.Headers[ServerRequestLoggingMiddleware.CorrelationIdHeader] = "invalid\r\nvalue";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.DoesNotContain('\r', context.TraceIdentifier);
        Assert.DoesNotContain('\n', context.TraceIdentifier);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("responded 500", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
