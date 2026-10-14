namespace AgenticChangeOps.Workflow;

/// <summary>A machine-readable code and a fixed sentence. Never carries model text.</summary>
public sealed record WorkflowReason(WorkflowReasonCode Code, string Message);
