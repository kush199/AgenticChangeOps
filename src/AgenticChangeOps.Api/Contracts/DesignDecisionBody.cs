namespace AgenticChangeOps.Api.Contracts;

/// <summary>Transport shape for a design decision; the identifier and version come from the route.</summary>
public sealed record DesignDecisionBody(string? ApproverName, string? Decision, string? Reason);
