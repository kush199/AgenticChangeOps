namespace AgenticChangeOps.Api.Contracts;

/// <summary>Transport shape for a decision; the identifier comes from the route.</summary>
public sealed record DecisionBody(string? ApproverName, string? Decision, string? Reason);
