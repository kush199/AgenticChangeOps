namespace AgenticChangeOps.Workflow;

/// <summary>Where a change request is in the fixed order. Always derived, never stored.</summary>
public enum WorkflowStage
{
    AwaitingRequirementsDraft,
    AwaitingDecision,
    ReadyForDesign,
    DesignComplete,
    Deferred,
    Rejected
}
