using Microsoft.Extensions.Logging;

namespace Modbuska.UnitTest;

/// <summary>
///     Test implementation of <see cref="ILogger{T}" /> that collects all log records in memory for assertions.
/// </summary>
public sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _logs = new();

    /// <summary>
    ///     Collected log records, in the order they were written.
    /// </summary>
    public IList<LogEntry> Logs => _logs;

    /// <summary>
    ///     Gets or sets whether logging is enabled. When <c>false</c>, <see cref="IsEnabled" /> returns <c>false</c>
    ///     for all levels, simulating a logger whose minimum level is above Trace.
    /// </summary>
    public bool IsLoggingEnabled { get; set; } = true;

    /// <summary>
    ///     Returns <see cref="IsLoggingEnabled" /> for all levels.
    /// </summary>
    public bool IsEnabled(LogLevel logLevel)
    {
        return IsLoggingEnabled;
    }

    /// <summary>
    ///     Scopes are not used by the tests; returns a no-op disposable.
    /// </summary>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _logs.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
    }

    /// <summary>
    ///     A single collected log record.
    /// </summary>
    public record LogEntry(LogLevel Level, EventId EventId, Exception? Exception, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        private NullScope()
        {
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>
///     Test implementation of <see cref="ILoggerProvider" /> that collects log records for assertions.
/// </summary>
public sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly TestLogger _logger = new();

    /// <summary>
    ///     Collected records in the order they were written: (category, level, message).
    /// </summary>
    public IList<(string Category, LogLevel Level, string Message)> Records => _logger.Records;

    public ILogger CreateLogger(string categoryName)
    {
        _logger.Category = categoryName;
        return _logger;
    }

    public void Dispose()
    {
    }
}

/// <summary>
///     Test implementation of <see cref="ILogger" /> used by <see cref="TestLoggerProvider" />.
/// </summary>
public sealed class TestLogger : ILogger
{
    private readonly List<(string Category, LogLevel Level, string Message)> _records = new();

    public string Category { get; set; } = string.Empty;

    public IList<(string Category, LogLevel Level, string Message)> Records => _records;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _records.Add((Category, logLevel, formatter(state, exception)));
    }
}