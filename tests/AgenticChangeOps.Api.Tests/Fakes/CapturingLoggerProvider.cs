using Microsoft.Extensions.Logging;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Collects every formatted log line (and exception text) for leak checks.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines
    {
        get { lock (_lines) return _lines.ToArray(); }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void Add(string line)
    {
        lock (_lines) _lines.Add(line);
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
            _owner.Add(text);
        }
    }
}
