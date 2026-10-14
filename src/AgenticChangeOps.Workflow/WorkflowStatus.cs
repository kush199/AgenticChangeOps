using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// A read-only view of one change request's lifecycle position, recomputed from recorded facts
/// on every call. Exactly one of <see cref="NextStep"/> and <see cref="Reason"/> is set.
/// </summary>
public sealed record WorkflowStatus(
    Guid ChangeRequestId,
    ChangeRequestStatus ChangeRequestStatus,
    WorkflowStage Stage,
    IReadOnlyList<CompletedStep> CompletedSteps,
    WorkflowStep? NextStep,
    WorkflowReason? Reason)
{
    // Value equality, with the completed steps compared element by element: a status read
    // twice from unchanged facts is equal even though each read builds its own list.
    public bool Equals(WorkflowStatus? other) =>
        other is not null
        && ChangeRequestId == other.ChangeRequestId
        && ChangeRequestStatus == other.ChangeRequestStatus
        && Stage == other.Stage
        && NextStep == other.NextStep
        && Equals(Reason, other.Reason)
        && CompletedSteps.SequenceEqual(other.CompletedSteps);

    public override int GetHashCode() =>
        HashCode.Combine(ChangeRequestId, ChangeRequestStatus, Stage, NextStep, Reason, CompletedSteps.Count);
}
