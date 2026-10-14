using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Workflow.Tests.Fakes;

/// <summary>Stand-in requirements generator: never calls a model. Counts calls.</summary>
public sealed class FakeRequirementsGenerator : IRequirementsDraftGenerator
{
    private int _callCount;

    public int CallCount => _callCount;

    public Func<ChangeRequest, CancellationToken, Task<DraftContent?>> Behavior { get; set; } =
        (_, _) => Task.FromResult<DraftContent?>(ValidContent());

    public Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Behavior(changeRequest, cancellationToken);
    }

    public static DraftContent ValidContent() => new(
        Summary: "Move the nightly sample report job to a quieter window.",
        FunctionalRequirements: new[] { "The report job runs at the newly configured time each night." },
        AcceptanceCriteria: new[] { "The report is produced within the new window on three consecutive nights." },
        Assumptions: new[] { "The new window has no competing batch jobs." },
        OpenQuestions: new[] { "Should weekend runs keep the old schedule?" });
}

/// <summary>Stand-in design generator: never calls a model. Counts calls.</summary>
public sealed class FakeDesignGenerator : IDesignDraftGenerator
{
    private int _callCount;

    public int CallCount => _callCount;

    public Func<ChangeRequest, RequirementsBasis, CancellationToken, Task<DesignContent?>> Behavior { get; set; } =
        (_, _, _) => Task.FromResult<DesignContent?>(ValidContent());

    public Task<DesignContent?> GenerateAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Behavior(changeRequest, basis, cancellationToken);
    }

    public static DesignContent ValidContent() => new(
        ApproachSummary: "Move the nightly report job by changing its schedule entry only.",
        AffectedComponents: new[] { "Report scheduler configuration" },
        ImpactAndRisks: new[] { "Reports arrive later for early-morning readers." },
        Dependencies: new[] { "Batch import finishes before the new window opens." },
        RollbackConsiderations: new[] { "Restore the previous schedule entry." },
        OpenQuestions: new[] { "Who confirms the new window with readers?" });
}
