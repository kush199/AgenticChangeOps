namespace AgenticChangeOps.Agents.Developer.Tests.Fakes;

/// <summary>Stand-in source of the latest requirements and design drafts. Counts every lookup.</summary>
public sealed class FakeProposalBasisSource : IProposalBasisSource
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ProposalBasis> _bases = new();
    private int _callCount;

    public int CallCount => _callCount;

    /// <summary>Runs on every lookup, before it answers; tests use it to change the drafts mid-request.</summary>
    public Action<Guid>? OnLookup { get; set; }

    public void Set(Guid changeRequestId, ProposalBasis basis)
    {
        lock (_gate)
            _bases[changeRequestId] = basis;
    }

    public ProposalBasis GetLatest(Guid changeRequestId)
    {
        Interlocked.Increment(ref _callCount);
        OnLookup?.Invoke(changeRequestId);
        lock (_gate)
            return _bases.TryGetValue(changeRequestId, out var basis) ? basis : new ProposalBasis(null, null);
    }

    public static ProposalRequirementsBasis Requirements(int version = 1) => new(
        Version: version,
        Summary: "Move the nightly Sample Billing Portal report job to a quieter window.",
        FunctionalRequirements: new[] { "The report job runs at the newly configured time each night." },
        AcceptanceCriteria: new[] { "The nightly report is produced within the new window." },
        Assumptions: new[] { "The new window has no competing batch jobs." },
        OpenQuestions: new[] { "Should weekend runs keep the old schedule?" });

    public static ProposalDesignBasis Design(int version = 1, int requirementsVersion = 1) => new(
        Version: version,
        RequirementsVersion: requirementsVersion,
        ApproachSummary: "Change the report hour in the portal settings only.",
        AffectedComponents: new[] { "Portal settings" },
        ImpactAndRisks: new[] { "Reports arrive later for early readers." },
        Dependencies: new[] { "Batch import finishes before the new hour." },
        RollbackConsiderations: new[] { "Restore the previous hour." },
        OpenQuestions: new[] { "Who confirms the new hour?" });

    public static ProposalBasis Current(int requirementsVersion = 1, int designVersion = 1) =>
        new(Requirements(requirementsVersion), Design(designVersion, requirementsVersion));
}
