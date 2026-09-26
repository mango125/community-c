using Community_C.Utility;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Community_C.Utility.Logs;

public static class ServerLoggingExtensions
{
    private const string DefaultTimeZoneId = "Asia/Seoul";
    private const int LogRetentionDays = 14;

    /// <summary>
    /// 서버의 콘솔·파일 로그 출력기와 로그 시간대를 등록한다.
    /// </summary>
    public static WebApplicationBuilder AddServerLogging(this WebApplicationBuilder builder)
    {
        // 서버 로그 파일 경로를 별도로 지정
        string? configuredLogPath = builder.Configuration["Logging:FilePath"];
        string logPath = ResolveLogPath(configuredLogPath);
        string? configuredTimeZoneId =
            builder.Configuration["Logging:TimeZoneId"];
        TimeZoneInfo logTimeZone = ResolveTimeZone(configuredTimeZoneId);

        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });
        //운영 시에만 JSON 콘솔 로깅을 사용하도록 설정
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            });
        }

        builder.Logging.AddProvider(new DailyJsonFileLoggerProvider(
            new FileManager(logPath, LogRetentionDays),
            logTimeZone));

        return builder;
    }

    /// <summary>
    /// HTTP 요청 로그 미들웨어를 애플리케이션 파이프라인에 등록한다.
    /// </summary>
    public static IApplicationBuilder UseServerRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ServerRequestLoggingMiddleware>();
    }

    /// <summary>
    /// 설정된 경로를 실행 파일 기준의 절대 로그 파일 경로로 변환한다.
    /// </summary>
    private static string ResolveLogPath(string? configuredLogPath)
    {
        if (string.IsNullOrWhiteSpace(configuredLogPath))
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                "logs",
                "community.log");
        }

        return Path.IsPathRooted(configuredLogPath)
            ? Path.GetFullPath(configuredLogPath)
            : Path.GetFullPath(configuredLogPath, AppContext.BaseDirectory);
    }

    /// <summary>
    /// 설정된 시간대를 확인하고 사용할 수 없으면 기본 시간대를 반환한다.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string? configuredTimeZoneId)
    {
        string requestedTimeZoneId = string.IsNullOrWhiteSpace(configuredTimeZoneId)
            ? DefaultTimeZoneId
            : configuredTimeZoneId.Trim();

        if (TryFindTimeZone(requestedTimeZoneId, out TimeZoneInfo timeZone))
        {
            return timeZone;
        }

        Console.Error.WriteLine(
            $"Logging:TimeZoneId is invalid. The default time zone '{DefaultTimeZoneId}' will be used.");

        return TryFindTimeZone(DefaultTimeZoneId, out timeZone)
            ? timeZone
            : TimeZoneInfo.Utc;
    }

    /// <summary>
    /// 운영체제에서 지정한 시간대 ID를 조회한다.
    /// </summary>
    private static bool TryFindTimeZone(
        string timeZoneId,
        out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}

public sealed class DailyJsonFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private const string OriginalFormatProperty = "{OriginalFormat}";

    private readonly object _writeLock = new();
    private readonly FileManager _fileManager;
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _timeProvider;

    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
    private StreamWriter? _writer;
    private DateOnly? _openedDate;
    private bool _disposed;
    private bool _writeFailureReported;

    /// <summary>
    /// 파일 관리자와 로그 시간대를 사용해 일자별 JSON 로그 출력기를 초기화한다.
    /// </summary>
    public DailyJsonFileLoggerProvider(
        FileManager fileManager,
        TimeZoneInfo timeZone,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(fileManager);
        ArgumentNullException.ThrowIfNull(timeZone);

        _fileManager = fileManager;
        _timeZone = timeZone;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 지정한 카테고리의 JSON 파일 로거를 생성한다.
    /// </summary>
    public ILogger CreateLogger(string categoryName)
    {
        return new DailyJsonFileLogger(categoryName, this);
    }

    /// <summary>
    /// 외부 로그 Scope 정보를 제공하는 객체를 설정한다.
    /// </summary>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// 열려 있는 로그 파일을 닫고 Provider 자원을 해제한다.
    /// </summary>
    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    /// <summary>
    /// 구조화 로그 정보를 JSON으로 변환해 현재 날짜의 로그 파일에 기록한다.
    /// </summary>
    private void Write<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        DateTimeOffset zonedTimestamp = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            _timeZone);
        DateOnly logDate = DateOnly.FromDateTime(zonedTimestamp.DateTime);

        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = zonedTimestamp.ToString("O", CultureInfo.InvariantCulture),
            ["level"] = logLevel.ToString(),
            ["category"] = categoryName,
            ["eventId"] = eventId.Id,
            ["eventName"] = eventId.Name,
            ["message"] = formatter(state, exception)
        };

        Dictionary<string, object?> properties = ExtractProperties(state);
        AddScopes(properties);
        if (properties.Count > 0)
        {
            entry["properties"] = properties;
        }

        if (exception is not null)
        {
            entry["exceptionType"] = exception.GetType().FullName;
            entry["stackTrace"] = exception.StackTrace;
        }

        string json = JsonSerializer.Serialize(entry);

        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                EnsureWriter(logDate);
                _writer!.WriteLine(json);
                _writeFailureReported = false;
            }
            catch (Exception writeException) when (
                writeException is IOException or UnauthorizedAccessException)
            {
                if (!_writeFailureReported)
                {
                    Console.Error.WriteLine(
                        $"File logging is unavailable. ExceptionType: {writeException.GetType().Name}");
                    _writeFailureReported = true;
                }
            }
        }
    }

    /// <summary>
    /// 로그 날짜가 변경되면 기존 파일을 닫고 해당 날짜 파일을 연다.
    /// </summary>
    private void EnsureWriter(DateOnly logDate)
    {
        if (_writer is not null && _openedDate == logDate)
        {
            return;
        }

        _writer?.Dispose();
        _writer = new StreamWriter(
            _fileManager.OpenLogFile(logDate),
            new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _openedDate = logDate;
    }

    /// <summary>
    /// LoggerMessage가 전달한 구조화 속성을 JSON 저장용 값으로 변환한다.
    /// </summary>
    private static Dictionary<string, object?> ExtractProperties<TState>(TState state)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (state is not IEnumerable<KeyValuePair<string, object?>> structuredState)
        {
            return properties;
        }

        foreach ((string key, object? value) in structuredState)
        {
            if (key != OriginalFormatProperty)
            {
                properties[key] = NormalizeValue(value);
            }
        }

        return properties;
    }

    /// <summary>
    /// 현재 로그 Scope의 구조화 속성을 로그 속성에 추가한다.
    /// </summary>
    private void AddScopes(Dictionary<string, object?> properties)
    {
        _scopeProvider.ForEachScope(
            static (scope, target) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    foreach ((string key, object? value) in values)
                    {
                        target.TryAdd(key, NormalizeValue(value));
                    }
                }
            },
            properties);
    }

    /// <summary>
    /// 구조화 로그 값을 JSON으로 안전하게 기록할 수 있는 기본 형식으로 변환한다.
    /// </summary>
    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or byte or sbyte or short or ushort or int or uint or
                long or ulong or float or double or decimal => value,
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString(),
            _ => value.ToString()
        };
    }

    private sealed class DailyJsonFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly DailyJsonFileLoggerProvider _provider;

        /// <summary>
        /// 카테고리와 Provider를 사용해 JSON 파일 로거를 초기화한다.
        /// </summary>
        public DailyJsonFileLogger(
            string categoryName,
            DailyJsonFileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        /// <summary>
        /// 현재 로그 작업에 구조화 Scope를 추가한다.
        /// </summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _provider._scopeProvider.Push(state);
        }

        /// <summary>
        /// 지정한 로그 레벨을 파일에 기록할 수 있는지 반환한다.
        /// </summary>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        /// <summary>
        /// 전달된 로그 상태와 예외 정보를 Provider에 기록하도록 요청한다.
        /// </summary>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                _provider.Write(
                    _categoryName,
                    logLevel,
                    eventId,
                    state,
                    exception,
                    formatter);
            }
        }
    }
}
