namespace AgenticChangeOps.Agents.Architect.Tests.Fakes;

/// <summary>Stand-in source of the latest requirements draft. Counts every lookup.</summary>
public sealed class FakeRequirementsBasisSource : IRequirementsBasisSource
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RequirementsBasis> _bases = new();
    private int _callCount;

    public int CallCount => _callCount;

    public void Set(Guid changeRequestId, RequirementsBasis basis)
    {
        lock (_gate)
            _bases[changeRequestId] = basis;
    }

    public RequirementsBasis? GetLatest(Guid changeRequestId)
    {
        Interlocked.Increment(ref _callCount);
        lock (_gate)
            return _bases.TryGetValue(changeRequestId, out var basis) ? basis : null;
    }

    /// <summary>A complete, fictional requirements draft at the given version.</summary>
    public static RequirementsBasis Basis(int version = 1) => new(
        Version: version,
        Summary: "Move the nightly Sample Billing Portal report job to a quieter window.",
        FunctionalRequirements: new[] { "The report job runs at the newly configured time each night." },
        AcceptanceCriteria: new[] { "The nightly report is produced within the new window." },
        Assumptions: new[] { "The new window has no competing batch jobs." },
        OpenQuestions: new[] { "Should weekend runs keep the old schedule?" });
}
