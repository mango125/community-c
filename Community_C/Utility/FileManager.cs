using System.Globalization;

namespace Community_C.Utility;

public sealed class FileManager
{
    private readonly string _directoryPath;
    private readonly string _fileNamePrefix;
    private readonly string _fileExtension;
    private readonly int _retentionDays;

    /// <summary>
    /// 기본 로그 경로와 보존 기간을 사용해 파일 관리자를 초기화한다.
    /// </summary>
    public FileManager(string baseFilePath, int retentionDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFilePath);

        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        }

        string fullPath = Path.GetFullPath(baseFilePath);
        _directoryPath = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException(
                "The log file path must include a directory.",
                nameof(baseFilePath));
        _fileNamePrefix = Path.GetFileNameWithoutExtension(fullPath);
        _fileExtension = string.IsNullOrWhiteSpace(Path.GetExtension(fullPath))
            ? ".log"
            : Path.GetExtension(fullPath);
        _retentionDays = retentionDays;
    }

    /// <summary>
    /// 지정한 날짜의 로그 파일을 확인하고 이어 쓰기 가능한 스트림을 반환한다.
    /// </summary>
    public FileStream OpenLogFile(DateOnly logDate)
    {
        Directory.CreateDirectory(_directoryPath);
        string datedFilePath = GetDatedLogFilePath(logDate);
        DeleteExpiredLogFiles(logDate);

        return new FileStream(
            datedFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            FileOptions.SequentialScan);
    }

    /// <summary>
    /// 지정한 날짜가 포함된 로그 파일의 절대 경로를 반환한다.
    /// </summary>
    public string GetDatedLogFilePath(DateOnly logDate)
    {
        return Path.Combine(
            _directoryPath,
            $"{_fileNamePrefix}-{logDate:yyyyMMdd}{_fileExtension}");
    }

    /// <summary>
    /// 현재 로그 날짜를 기준으로 보존 기간을 지난 로그 파일을 삭제한다.
    /// </summary>
    private void DeleteExpiredLogFiles(DateOnly currentDate)
    {
        DateOnly oldestDateToKeep = currentDate.AddDays(-(_retentionDays - 1));
        string searchPattern = $"{_fileNamePrefix}-????????{_fileExtension}";

        foreach (string filePath in Directory.EnumerateFiles(
                     _directoryPath,
                     searchPattern,
                     SearchOption.TopDirectoryOnly))
        {
            if (!TryGetLogDate(filePath, out DateOnly fileDate) ||
                fileDate >= oldestDateToKeep)
            {
                continue;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(
                    $"An expired log file could not be deleted. ExceptionType: {exception.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// 일자별 로그 파일명에서 날짜를 추출한다.
    /// </summary>
    private bool TryGetLogDate(string filePath, out DateOnly fileDate)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string dateText = fileName[(_fileNamePrefix.Length + 1)..];

        return DateOnly.TryParseExact(
            dateText,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out fileDate);
    }
}
