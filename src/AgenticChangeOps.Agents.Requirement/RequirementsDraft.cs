using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Requirement;

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
    IReadOnlyList<string> OpenQuestions) : IAgentOutput
{
    public const string RequirementAgentName = "RequirementAgent";
}
