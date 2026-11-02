using System.Text.Json.Serialization;
using AgenticChangeOps.Agents.Architect;

namespace AgenticChangeOps.Api.Contracts;

/// <summary>What a person decided about a design, as shown with the design.</summary>
public sealed record DesignDecisionView(
    DesignDecisionOutcome Outcome,
    string ApproverName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Reason,
    DateTime DecidedAt);

/// <summary>
/// A stored design draft plus, once a person has decided it, that decision. Every design member
/// keeps its name, order and serialization condition, so a design with no decision serializes
/// exactly as the stored draft does.
/// </summary>
public sealed record DesignView(
    Guid DesignId,
    Guid ChangeRequestId,
    int Version,
    int RequirementsVersion,
    DateTime GeneratedAt,
    DesignDraftStatus Status,
    string AgentName,
    string ApproachSummary,
    IReadOnlyList<string> AffectedComponents,
    IReadOnlyList<string> ImpactAndRisks,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> RollbackConsiderations,
    IReadOnlyList<string> OpenQuestions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? SampleFilesRead = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DesignDecisionView? Decision = null)
{
    public static DesignView From(DesignDraft design, DesignDecision? decision) => new(
        design.DesignId,
        design.ChangeRequestId,
        design.Version,
        design.RequirementsVersion,
        design.GeneratedAt,
        design.Status,
        design.AgentName,
        design.ApproachSummary,
        design.AffectedComponents,
        design.ImpactAndRisks,
        design.Dependencies,
        design.RollbackConsiderations,
        design.OpenQuestions,
        design.SampleFilesRead,
        decision is null
            ? null
            : new DesignDecisionView(decision.Outcome, decision.ApproverName, decision.Reason, decision.DecidedAt));
}
