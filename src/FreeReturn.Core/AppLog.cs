using System.Text;
using Microsoft.Extensions.Logging;

namespace FreeReturn.Core;

public enum LogLevelKind
{
    Debug,
    Info,
    Warning,
    Error,
}

public sealed record LogEntry(DateTimeOffset Time, LogLevelKind Level, string Message)
{
    public override string ToString() => $"{Time:HH:mm:ss} [{Level}] {Message}";
}

/// <summary>
/// Small file logger. Writes one file per day to the logs folder and keeps the last two weeks.
/// Callers must never pass secrets (passwords, codes, passphrases) to it.
/// </summary>
public static class AppLog
{
    private const int RetainDays = 14;
    private static readonly Lock Gate = new();
    private static string? _directory;

    public static event Action<LogEntry>? EntryWritten;

    public static string? Directory => _directory;

    public static void Initialize(string directory)
    {
        _directory = directory;
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("FreeReturn-*.log"))
            {
                if (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-RetainDays))
                    file.Delete();
            }
        }
        catch (IOException)
        {
        }
    }

    public static void Debug(string message) => Write(LogLevelKind.Debug, message);

    public static void Info(string message) => Write(LogLevelKind.Info, message);

    public static void Warn(string message, Exception? ex = null) => Write(LogLevelKind.Warning, Append(message, ex));

    public static void Error(string message, Exception? ex = null) => Write(LogLevelKind.Error, Append(message, ex));

    public static ILogger CreateLibraryLogger(string category) => new LibraryLogger(category);

    private static string Append(string message, Exception? ex) => ex is null ? message : $"{message}{Environment.NewLine}{ex}";

    private static void Write(LogLevelKind level, string message)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, message);
        if (_directory is not null)
        {
            lock (Gate)
            {
                try
                {
                    var file = Path.Combine(_directory, $"FreeReturn-{entry.Time:yyyyMMdd}.log");
                    File.AppendAllText(file, $"{entry.Time:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        EntryWritten?.Invoke(entry);
    }

    /// <summary>Routes Netimobiledevice diagnostics into the app log (warnings and above only).</summary>
    private sealed class LibraryLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var text = $"{category}: {formatter(state, exception)}";
            if (logLevel >= LogLevel.Error)
                Error(text, exception);
            else
                Warn(text, exception);
        }
    }
}
