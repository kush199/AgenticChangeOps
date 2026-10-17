using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic.Support;

/// <summary>
/// The workflow test fixture plus a workflow service wired to the automatic-status source and the
/// automatic-advancement service under test. Time comes from the fake clock; nothing sleeps.
/// </summary>
public sealed class AutomaticFixture
{
    public WorkflowTestFixture Base { get; } = new();
    public AutomaticAdvancementSettings Settings { get; }
    public WorkflowService Workflow { get; }
    public AutomaticAdvancementService Automatic { get; }

    public AutomaticFixture(AutomaticAdvancementSettings? settings = null, IStepAvailability? availability = null)
    {
        Settings = settings ?? On();
        var steps = availability ?? Base.Availability;
        var source = new AutomaticStatusSource(Base.Store, Settings);
        Workflow = new WorkflowService(Base.Store, Base.Requirements, Base.Designs, steps, source);
        Automatic = new AutomaticAdvancementService(Base.Store, Workflow, Workflow, Base.Clock, Settings, steps);
    }

    public IChangeRequestStore Store => Base.Store;

    public static AutomaticAdvancementSettings On(
        int maxSteps = 5, int maxFailures = 3, int failureWaitSeconds = 900) =>
        new(Enabled: true,
            Interval: TimeSpan.FromMinutes(5),
            MaxStepsPerCheck: maxSteps,
            FailureWait: TimeSpan.FromSeconds(failureWaitSeconds),
            MaxConsecutiveFailures: maxFailures);

    public static AutomaticAdvancementSettings Off() => On() with { Enabled = false };

    public void Advance(TimeSpan by) => Base.Clock.Now += by;

    public WorkflowStatus Status(Guid id) => Workflow.GetStatus(id).Value!;

    /// <summary>Makes the requirements generator fail with a category.</summary>
    public void RequirementsFail(GenerationFailureCategory category = GenerationFailureCategory.Unreachable) =>
        Base.RequirementsGenerator.Behavior = (_, _) => throw new DraftGenerationException(category);

    public void RequirementsSucceed() =>
        Base.RequirementsGenerator.Behavior = (_, _) =>
            Task.FromResult<DraftContent?>(FakeRequirementsGenerator.ValidContent());

    /// <summary>Makes the design generator fail with a category.</summary>
    public void DesignFail(GenerationFailureCategory category = GenerationFailureCategory.Unreachable) =>
        Base.DesignGenerator.Behavior = (_, _, _) => throw new DraftGenerationException(category);
}

/// <summary>Reports "configured" except that its first call throws, standing in for an unexpected error.</summary>
public sealed class ThrowOnFirstCallAvailability : IStepAvailability
{
    private int _calls;

    public bool IsConfigured(WorkflowStep step)
    {
        if (Interlocked.Increment(ref _calls) == 1)
            throw new InvalidOperationException("synthetic unexpected error");
        return true;
    }
}
