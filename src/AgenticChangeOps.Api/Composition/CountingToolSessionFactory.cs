using System.Diagnostics;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Wraps the real session factory so that, when a design request is finished, one log line states
/// how many tool calls were made and how many were refused, by outcome name, and how long the
/// request took. Counts only: no path, search term, file content or setting value ever reaches the
/// log. This is also the only record of a request that failed and so stored nothing.
/// </summary>
public sealed class CountingToolSessionFactory : IToolSessionFactory
{
    private readonly IToolSessionFactory _inner;
    private readonly ILogger _logger;

    public CountingToolSessionFactory(IToolSessionFactory inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<IToolSession> OpenAsync(CancellationToken cancellationToken)
    {
        var session = await _inner.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new CountingSession(session, _logger);
    }

    private sealed class CountingSession : IToolSession
    {
        private readonly IToolSession _inner;
        private readonly ILogger _logger;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _logged;

        public CountingSession(IToolSession inner, ILogger logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public ToolSet Tools => _inner.Tools;
        public IReadOnlyList<ToolCallRecord> Calls => _inner.Calls;
        public IReadOnlyList<string> FilesRead => _inner.FilesRead;
        public int MaxModelCalls => _inner.MaxModelCalls;
        public TimeSpan TimeLimit => _inner.TimeLimit;

        public async ValueTask DisposeAsync()
        {
            if (!_logged)
            {
                _logged = true;
                var calls = _inner.Calls;
                var refused = calls.Where(c => c.Outcome != ToolCallOutcome.Served)
                    .GroupBy(c => c.Outcome)
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key}={g.Count()}");
                _logger.LogInformation(
                    "Design tool session finished: {Calls} calls, {Refused} refused ({Outcomes}), {ElapsedMs} ms.",
                    calls.Count,
                    calls.Count(c => c.Outcome != ToolCallOutcome.Served),
                    string.Join(", ", refused),
                    _clock.ElapsedMilliseconds);
            }

            await _inner.DisposeAsync().ConfigureAwait(false);
        }
    }
}
