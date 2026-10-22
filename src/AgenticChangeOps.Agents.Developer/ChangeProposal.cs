using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>One proposed edit. Text only: nothing here is ever applied.</summary>
public sealed record ProposedChange(
    string Path,
    ProposalChangeKind Kind,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? ExistingText,
    string ReplacementText);

public sealed record ChangeProposal(
    Guid ProposalId,
    Guid ChangeRequestId,
    int Version,
    int RequirementsVersion,
    int DesignVersion,
    DateTime GeneratedAt,
    ProposalStatus Status,
    string AgentName,
    string Summary,
    IReadOnlyList<ProposedChange> Changes,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> TestIdeas,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> SampleFilesRead,
    string VerificationNote) : IAgentOutput
{
    public const string DeveloperAgentName = "DeveloperAgent";
    public const string VerificationNoteText = "Not compiled, run or tested.";
}
