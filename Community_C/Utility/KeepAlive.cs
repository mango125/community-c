namespace Community_C.Utility
{
    using Community_C.Models;
    using Community_C.Utility.Logs;
    using Microsoft.EntityFrameworkCore;

    public sealed class KeepAlive : BackgroundService
    {
        private const string DefaultTimeZoneId = "Asia/Seoul";

        private static readonly TimeOnly[] DefaultExecutionTimes =
        [
            new(9, 0),
            new(15, 0),
            new(21, 0)
        ];

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KeepAlive> _logger;
        private readonly TimeZoneInfo _timeZone;
        private readonly IReadOnlyList<TimeOnly> _executionTimes;
        private readonly bool _enabled;

        /// <summary>
        /// DB 확인 작업에 필요한 서비스와 User Secrets 기반 실행 설정을 초기화한다.
        /// </summary>
        public KeepAlive(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<KeepAlive> logger)
        {
            ArgumentNullException.ThrowIfNull(scopeFactory);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = configuration.GetValue("KeepAlive:Enabled", true);
            _timeZone = ResolveTimeZone(configuration["KeepAlive:TimeZoneId"]);
            _executionTimes = ResolveExecutionTimes(
                configuration.GetSection("KeepAlive:Times").Get<string[]>());
        }

        /// <summary>
        /// 다음 지정 시각까지 대기한 뒤 서버가 종료될 때까지 DB 확인 작업을 반복한다.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay = GetDelayUntilNextExecution(DateTimeOffset.UtcNow);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await CheckDatabaseAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 별도의 DI Scope에서 User 테이블의 전체 사용자 수를 조회한다.
        /// </summary>
        private async Task CheckDatabaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                DataContext db = scope.ServiceProvider
                    .GetRequiredService<DataContext>();
                int userCount = await db.user.CountAsync(cancellationToken);

                ServerLog.DatabaseKeepAliveSucceeded(_logger, userCount);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ServerLog.DatabaseKeepAliveFailed(_logger, exception);
            }
        }

        /// <summary>
        /// 현재 UTC 시각을 기준으로 다음 DB 확인 시각까지의 대기 시간을 계산한다.
        /// </summary>
        private TimeSpan GetDelayUntilNextExecution(DateTimeOffset utcNow)
        {
            DateTimeOffset zonedNow = TimeZoneInfo.ConvertTime(utcNow, _timeZone);
            DateOnly localDate = DateOnly.FromDateTime(zonedNow.DateTime);

            foreach (TimeOnly executionTime in _executionTimes)
            {
                DateTime localCandidate = localDate.ToDateTime(
                    executionTime,
                    DateTimeKind.Unspecified);

                if (localCandidate > zonedNow.DateTime)
                {
                    return ConvertToUtc(localCandidate) - utcNow;
                }
            }

            DateTime nextDayCandidate = localDate
                .AddDays(1)
                .ToDateTime(_executionTimes[0], DateTimeKind.Unspecified);

            return ConvertToUtc(nextDayCandidate) - utcNow;
        }

        /// <summary>
        /// 지정된 현지 시각을 KeepAlive 시간대 기준의 UTC 시각으로 변환한다.
        /// </summary>
        private DateTimeOffset ConvertToUtc(DateTime localDateTime)
        {
            TimeSpan utcOffset = _timeZone.GetUtcOffset(localDateTime);
            return new DateTimeOffset(localDateTime, utcOffset).ToUniversalTime();
        }

        /// <summary>
        /// 설정된 시간대 ID를 확인하고 사용할 수 없으면 Asia/Seoul 시간대를 반환한다.
        /// </summary>
        private static TimeZoneInfo ResolveTimeZone(string? configuredTimeZoneId)
        {
            string timeZoneId = string.IsNullOrWhiteSpace(configuredTimeZoneId)
                ? DefaultTimeZoneId
                : configuredTimeZoneId.Trim();

            if (TryFindTimeZone(timeZoneId, out TimeZoneInfo timeZone))
            {
                return timeZone;
            }

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

        /// <summary>
        /// 설정된 실행 시각을 변환하고 값이 없거나 잘못되면 기본 실행 시각을 반환한다.
        /// </summary>
        private static IReadOnlyList<TimeOnly> ResolveExecutionTimes(
            IEnumerable<string>? configuredTimes)
        {
            if (configuredTimes is null)
            {
                return DefaultExecutionTimes;
            }

            TimeOnly[] executionTimes = configuredTimes
                .Select(value => TimeOnly.TryParse(value, out TimeOnly parsedTime)
                    ? (TimeOnly?)parsedTime
                    : null)
                .Where(time => time.HasValue)
                .Select(time => time!.Value)
                .Distinct()
                .OrderBy(time => time)
                .ToArray();

            return executionTimes.Length > 0
                ? executionTimes
                : DefaultExecutionTimes;
        }
    }
}
