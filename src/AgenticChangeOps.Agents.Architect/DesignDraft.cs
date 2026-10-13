using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect;

public sealed record DesignDraft(
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
    IReadOnlyList<string> OpenQuestions) : IAgentOutput
{
    public const string ArchitectAgentName = "ArchitectAgent";
}
