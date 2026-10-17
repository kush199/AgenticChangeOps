namespace AgenticChangeOps.Workflow;

/// <summary>Keeps the latest check summary in memory; null until a check has run since start.</summary>
public sealed class CheckSummaryHolder
{
    private readonly object _lock = new();
    private CheckSummary? _latest;

    public CheckSummary? Latest
    {
        get
        {
            lock (_lock)
                return _latest;
        }
    }

    public void Record(CheckSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        lock (_lock)
            _latest = summary;
    }
}
