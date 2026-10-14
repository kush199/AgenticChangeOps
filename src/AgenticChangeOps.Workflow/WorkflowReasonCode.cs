namespace AgenticChangeOps.Workflow;

/// <summary>Why no step can run.</summary>
public enum WorkflowReasonCode
{
    AwaitingDecision,
    Deferred,
    Rejected,
    RequirementsDraftRequired,
    NothingDue
}
