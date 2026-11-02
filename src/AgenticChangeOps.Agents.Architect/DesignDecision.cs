using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>What the approver chose. Request input only; the stored form is <see cref="DesignDecisionOutcome"/>.</summary>
public enum DesignVerdict
{
    Accept,
    Reject
}

public enum DesignDecisionOutcome
{
    Accepted,
    Rejected
}

/// <summary>
/// A person's decision on one design draft version. Stored once, never edited or deleted. Version
/// is the design version decided, so the store's one-output-per-version rule makes it exclusive.
/// </summary>
public sealed record DesignDecision(
    Guid DecisionId,
    Guid ChangeRequestId,
    int Version,
    Guid DesignId,
    DesignDecisionOutcome Outcome,
    string ApproverName,
    string? Reason,
    DateTime DecidedAt) : IAgentOutput;
