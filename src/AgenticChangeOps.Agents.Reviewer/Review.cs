using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>One entry in a list of the requirements, design or proposal; Position is 1-based.</summary>
public sealed record ReviewReference(ReviewDocument Document, ReferenceList List, int Position);

/// <summary>The system's result for one proposed edit; ChangeNumber is the edit's 1-based position.</summary>
public sealed record EditCheck(int ChangeNumber, EditCheckOutcome Outcome);

/// <summary>
/// The system's own re-check of the proposal edits against the sample. Edits are empty when the
/// check was not performed or the proposal has no edits.
/// </summary>
public sealed record SystemCheck(SystemCheckStatus Status, IReadOnlyList<EditCheck> Edits);

public sealed record ReviewFinding(
    FindingCategory Category,
    FindingSeverity Severity,
    string Statement,
    IReadOnlyList<ReviewReference> References);

public sealed record Review(
    Guid ReviewId,
    Guid ChangeRequestId,
    int Version,
    int RequirementsVersion,
    int DesignVersion,
    int ProposalVersion,
    DateTime GeneratedAt,
    ReviewStatus Status,
    string AgentName,
    SystemCheck SystemCheck,
    IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<string> OpenQuestions,
    string AdvisoryNote) : IAgentOutput
{
    public const string ReviewerAgentName = "ReviewerAgent";
    public const string AdvisoryNoteText = "Advisory only. Nothing acts on this review.";
}
