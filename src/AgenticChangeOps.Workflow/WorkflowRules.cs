using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// The fixed step order, as a pure function of recorded facts. Nothing here reads a store,
/// calls a service or consults a model; the same facts always give the same status.
/// </summary>
public static class WorkflowRules
{
    public static WorkflowStatus Derive(
        Guid changeRequestId,
        ChangeRequestStatus status,
        IReadOnlyList<RequirementsFact> requirements,
        IReadOnlyList<DesignFact> designs)
    {
        var orderedRequirements = requirements.OrderBy(r => r.Version).ToArray();
        var orderedDesigns = designs.OrderBy(d => d.Version).ToArray();

        var completed = orderedRequirements
            .Select(r => new CompletedStep(WorkflowStep.RequirementsDraft, r.Version, r.GeneratedAt, null))
            .Concat(orderedDesigns.Select(d =>
                new CompletedStep(WorkflowStep.DesignDraft, d.Version, d.GeneratedAt, d.RequirementsVersion)))
            .ToArray();

        var hasRequirements = orderedRequirements.Length > 0;

        WorkflowStatus Next(WorkflowStage stage, WorkflowStep step) =>
            new(changeRequestId, status, stage, completed, step, null);

        WorkflowStatus Stop(WorkflowStage stage, WorkflowReasonCode code, string message) =>
            new(changeRequestId, status, stage, completed, null, new WorkflowReason(code, message));

        switch (status)
        {
            case ChangeRequestStatus.Rejected:
                return Stop(WorkflowStage.Rejected, WorkflowReasonCode.Rejected,
                    "The change request is Rejected; no further step runs.");

            case ChangeRequestStatus.Deferred:
                return hasRequirements
                    ? Stop(WorkflowStage.Deferred, WorkflowReasonCode.Deferred,
                        "The change request is Deferred; no step beyond the requirements draft runs.")
                    : Next(WorkflowStage.Deferred, WorkflowStep.RequirementsDraft);

            case ChangeRequestStatus.Submitted:
                return hasRequirements
                    ? Stop(WorkflowStage.AwaitingDecision, WorkflowReasonCode.AwaitingDecision,
                        "The change request is Submitted and awaits a human decision.")
                    : Next(WorkflowStage.AwaitingRequirementsDraft, WorkflowStep.RequirementsDraft);

            case ChangeRequestStatus.UnderReview:
                return Stop(WorkflowStage.AwaitingDecision, WorkflowReasonCode.AwaitingDecision,
                    "The change request is UnderReview and awaits a human decision.");

            case ChangeRequestStatus.Approved:
                if (!hasRequirements)
                    return Stop(WorkflowStage.AwaitingRequirementsDraft, WorkflowReasonCode.RequirementsDraftRequired,
                        "The change request is Approved but has no requirements draft, which a design requires.");

                var latestRequirements = orderedRequirements[^1].Version;
                var designIsCurrent = orderedDesigns.Length > 0
                    && orderedDesigns[^1].RequirementsVersion == latestRequirements;

                return designIsCurrent
                    ? Stop(WorkflowStage.DesignComplete, WorkflowReasonCode.NothingDue,
                        "The latest design draft is based on the latest requirements draft; nothing further is due.")
                    : Next(WorkflowStage.ReadyForDesign, WorkflowStep.DesignDraft);

            default:
                // An unknown status is never advanced.
                return Stop(WorkflowStage.AwaitingDecision, WorkflowReasonCode.AwaitingDecision,
                    "The change request is in a status the workflow does not advance.");
        }
    }
}
