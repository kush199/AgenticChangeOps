using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// The outcome of one advance. On <see cref="AdvanceOutcome.Advanced"/> and
/// <see cref="AdvanceOutcome.StepFailed"/> the agent's own result object is carried unchanged.
/// </summary>
public sealed class AdvanceResult
{
    public AdvanceOutcome Outcome { get; }
    public WorkflowStep? Step { get; }
    public DraftingResult? Requirements { get; }
    public DesignDraftingResult? Design { get; }

    /// <summary>State after the call. Null only for <see cref="AdvanceOutcome.NotFound"/>.</summary>
    public WorkflowStatus? Status { get; }

    /// <summary>Set only for <see cref="AdvanceOutcome.NotFound"/>.</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    private AdvanceResult(
        AdvanceOutcome outcome,
        WorkflowStep? step,
        DraftingResult? requirements,
        DesignDraftingResult? design,
        WorkflowStatus? status,
        IReadOnlyList<ValidationError>? errors)
    {
        Outcome = outcome;
        Step = step;
        Requirements = requirements;
        Design = design;
        Status = status;
        Errors = errors ?? Array.Empty<ValidationError>();
    }

    public static AdvanceResult Advanced(DraftingResult result, WorkflowStatus status) =>
        new(AdvanceOutcome.Advanced, WorkflowStep.RequirementsDraft, result, null, status, null);

    public static AdvanceResult Advanced(DesignDraftingResult result, WorkflowStatus status) =>
        new(AdvanceOutcome.Advanced, WorkflowStep.DesignDraft, null, result, status, null);

    public static AdvanceResult StepFailed(DraftingResult result, WorkflowStatus status) =>
        new(AdvanceOutcome.StepFailed, WorkflowStep.RequirementsDraft, result, null, status, null);

    public static AdvanceResult StepFailed(DesignDraftingResult result, WorkflowStatus status) =>
        new(AdvanceOutcome.StepFailed, WorkflowStep.DesignDraft, null, result, status, null);

    public static AdvanceResult NothingDue(WorkflowStatus status) =>
        new(AdvanceOutcome.NothingDue, null, null, null, status, null);

    public static AdvanceResult NotConfigured(WorkflowStep step, WorkflowStatus status) =>
        new(AdvanceOutcome.NotConfigured, step, null, null, status, null);

    public static AdvanceResult NotFound(IReadOnlyList<ValidationError> errors) =>
        new(AdvanceOutcome.NotFound, null, null, null, null, errors);
}
