namespace AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

/// <summary>
/// Stand-in source of the latest requirements, design and proposal. Counts every lookup. The
/// builders give lists of known lengths so tests can point a reference at the first entry, the
/// last entry and one past the end.
/// </summary>
public sealed class FakeReviewBasisSource : IReviewBasisSource
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ReviewBases> _bases = new();
    private int _callCount;

    public int CallCount => _callCount;

    /// <summary>Runs on every lookup, before it answers; tests use it to change the upstream records mid-request.</summary>
    public Action<Guid>? OnLookup { get; set; }

    public void Set(Guid changeRequestId, ReviewBases bases)
    {
        lock (_gate)
            _bases[changeRequestId] = bases;
    }

    public ReviewBases GetLatest(Guid changeRequestId)
    {
        Interlocked.Increment(ref _callCount);
        OnLookup?.Invoke(changeRequestId);
        lock (_gate)
            return _bases.TryGetValue(changeRequestId, out var bases) ? bases : new ReviewBases(null, null, null);
    }

    public const string SettingsPath = "billing-portal/config/portal.settings";
    public const string SettingsQuote = "report_hour = 2";

    /// <summary>Lengths: functional requirements 2, acceptance criteria 2, assumptions 1, open questions 1.</summary>
    public static ReviewRequirementsBasis Requirements(int version = 1) => new(
        Version: version,
        Summary: "Move the nightly Sample Billing Portal report job to a quieter window.",
        FunctionalRequirements: new[]
        {
            "The report job runs at the newly configured time each night.",
            "The previous time is no longer used."
        },
        AcceptanceCriteria: new[]
        {
            "The nightly report is produced within the new window.",
            "No report is produced at the old hour."
        },
        Assumptions: new[] { "The new window has no competing batch jobs." },
        OpenQuestions: new[] { "Should weekend runs keep the old schedule?" });

    /// <summary>Lengths: affected components 2, impact and risks 1, dependencies 1, rollback 1, open questions 1.</summary>
    public static ReviewDesignBasis Design(int version = 1, int requirementsVersion = 1) => new(
        Version: version,
        RequirementsVersion: requirementsVersion,
        ApproachSummary: "Change the report hour in the portal settings only.",
        AffectedComponents: new[] { "Portal settings", "Nightly report job" },
        ImpactAndRisks: new[] { "Reports arrive later for early readers." },
        Dependencies: new[] { "Batch import finishes before the new hour." },
        RollbackConsiderations: new[] { "Restore the previous hour." },
        OpenQuestions: new[] { "Who confirms the new hour?" });

    /// <summary>Lengths: changes 3, risks 2, test ideas 1, open questions 1.</summary>
    public static ReviewProposalBasis Proposal(int version = 1, int requirementsVersion = 1, int designVersion = 1) => new(
        Version: version,
        RequirementsVersion: requirementsVersion,
        DesignVersion: designVersion,
        Summary: "Change the report hour and note it in the schedule file.",
        Changes: new[]
        {
            new ReviewProposedChange(SettingsPath, ReviewChangeKind.Change, SettingsQuote, "report_hour = 4"),
            new ReviewProposedChange("billing-portal/config/schedule.notes", ReviewChangeKind.Add, null, "report hour is 4\n"),
            new ReviewProposedChange("billing-portal/src/ReportJob", ReviewChangeKind.Change, "run at configured hour", "run at configured hour (see notes)")
        },
        Risks: new[] { "A reader in another time zone sees the report late.", "The notes file may drift from the settings." },
        TestIdeas: new[] { "Run the job at the new hour in a sandbox." },
        OpenQuestions: new[] { "Should the old hour stay as a fallback?" });

    public static ReviewBases Current(int requirementsVersion = 1, int designVersion = 1, int proposalVersion = 1) => new(
        Requirements(requirementsVersion),
        Design(designVersion, requirementsVersion),
        Proposal(proposalVersion, requirementsVersion, designVersion));

    /// <summary>A sample that holds the files the proposal above edits, with the quotes present exactly once.</summary>
    public static FakeReviewSampleFiles MatchingSample() => new FakeReviewSampleFiles()
        .With(SettingsPath, "page_size = 20\nreport_hour = 2\nretry_count = 3\n")
        .With("billing-portal/src/ReportJob", "start job\nrun at configured hour\nstop job\n");
}
