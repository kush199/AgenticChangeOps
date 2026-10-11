using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Models;

public sealed record RequirementsDraft(
    Guid DraftId,
    Guid ChangeRequestId,
    int Version,
    DateTime GeneratedAt,
    DraftStatus Status,
    string AgentName,
    string Summary,
    IReadOnlyList<string> FunctionalRequirements,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions)
{
    public const string RequirementAgentName = "RequirementAgent";
}
