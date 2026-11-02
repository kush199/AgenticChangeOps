using Microsoft.Extensions.Logging;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Collects every formatted log line (and exception text) for leak checks.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _lines = new();
    private readonly List<(LogLevel Level, string Line)> _levelled = new();

    public IReadOnlyList<string> Lines
    {
        get { lock (_lines) return _lines.ToArray(); }
    }

    /// <summary>Every line with its level, for checks such as "nothing at warning or above".</summary>
    public IReadOnlyList<(LogLevel Level, string Line)> LevelledLines
    {
        get { lock (_lines) return _levelled.ToArray(); }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void Add(LogLevel level, string line)
    {
        lock (_lines)
        {
            _lines.Add(line);
            _levelled.Add((level, line));
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly CapturingLoggerProvider _owner;

        public CapturingLogger(string category, CapturingLoggerProvider owner)
        {
            _category = category;
            _owner = owner;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = $"{_category}: {formatter(state, exception)}";
            if (exception is not null)
                text += $" | {exception}";
            _owner.Add(logLevel, text);
        }
    }
}
