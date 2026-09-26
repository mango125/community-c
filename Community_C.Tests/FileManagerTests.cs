using Community_C.Utility;
using Community_C.Utility.Logs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Community_C.Tests;

public class FileManagerTests
{
    [Fact]
    public void Logger_UsesConfiguredTimeZoneAndDeletesFilesOutsideRetentionWindow()
    {
        string testRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "test-logs",
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(testRoot);

        try
        {
            TimeZoneInfo koreaTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                "Test/Korea",
                TimeSpan.FromHours(9),
                "Test Korea Time",
                "Test Korea Time");
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            DateOnly currentDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(nowUtc, koreaTimeZone).DateTime);
            DateOnly expiredDate = currentDate.AddDays(-15);
            DateOnly retainedDate = currentDate.AddDays(-13);
            var fileManager = new FileManager(
                Path.Combine(testRoot, "community.log"),
                retentionDays: 14);
            string expiredFile = fileManager.GetDatedLogFilePath(expiredDate);
            string retainedFile = fileManager.GetDatedLogFilePath(retainedDate);
            File.WriteAllText(expiredFile, "expired");
            File.WriteAllText(retainedFile, "retained");
            var timeProvider = new TestTimeProvider(nowUtc);

            using (var provider = new DailyJsonFileLoggerProvider(
                       fileManager,
                       koreaTimeZone,
                       timeProvider: timeProvider))
            {
                ILogger logger = provider.CreateLogger("Community_C.Tests");
                logger.LogInformation("First daily log");

                string firstDailyFile = fileManager.GetDatedLogFilePath(currentDate);
                Assert.True(File.Exists(firstDailyFile));
                using JsonDocument logEntry = JsonDocument.Parse(
                    ReadWhileLoggerIsRunning(firstDailyFile));
                DateTimeOffset timestamp = DateTimeOffset.Parse(
                    logEntry.RootElement.GetProperty("timestamp").GetString()!);
                Assert.Equal(TimeSpan.FromHours(9), timestamp.Offset);
                Assert.False(File.Exists(expiredFile));
                Assert.True(File.Exists(retainedFile));

                timeProvider.SetUtcNow(nowUtc.AddDays(1));
                logger.LogInformation("Second daily log");

                Assert.True(File.Exists(fileManager.GetDatedLogFilePath(
                    currentDate.AddDays(1))));
            }
        }
        finally
        {
            string testLogsRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "test-logs"));
            if (testRoot.StartsWith(testLogsRoot, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static string ReadWhileLoggerIsRunning(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }
}
