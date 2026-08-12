using System.Text;
using ZJS2310.Core.Abstractions;

namespace ZJS2310.App.Infrastructure;

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message);

public sealed class FileLogger : IAppLogger
{
    private readonly string _directory;
    private readonly object _sync = new();

    public FileLogger(string directory) => _directory = directory;

    public event EventHandler<LogEntry>? EntryWritten;

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, message);
        var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}";
        lock (_sync)
        {
            Directory.CreateDirectory(_directory);
            File.AppendAllText(Path.Combine(_directory, $"ZJS2310_{DateTime.Now:yyyyMMdd}.log"), line, Encoding.UTF8);
        }

        EntryWritten?.Invoke(this, entry);
    }
}
