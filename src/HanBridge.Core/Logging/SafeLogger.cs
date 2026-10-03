using System.Text;

namespace HanBridge.Core.Logging;

public sealed class SafeLogger
{
    private const long MaxLogBytes = 1024 * 1024;
    private readonly string _logFilePath;
    private readonly object _gate = new();

    public SafeLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, "hanbridge.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            RotateIfNeeded();
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {Sanitize(message)}{Environment.NewLine}";
            File.AppendAllText(_logFilePath, line, Encoding.UTF8);
        }
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(_logFilePath);
        if (!file.Exists || file.Length < MaxLogBytes)
        {
            return;
        }

        var archive = Path.Combine(file.DirectoryName!, $"hanbridge-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(_logFilePath, archive, true);
    }

    private static string Sanitize(string message)
    {
        return message.Replace('\r', ' ').Replace('\n', ' ');
    }
}