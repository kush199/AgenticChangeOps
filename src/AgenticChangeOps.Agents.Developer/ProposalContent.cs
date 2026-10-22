using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>One edit as the model wrote it, before validation. Kind is the raw word.</summary>
public sealed record ProposedChangeContent(string? Path, string? Kind, string? ExistingText, string? ReplacementText);

/// <summary>
/// Raw output of the content generator, before validation. A null member means the section was
/// absent; an empty list means it was present but empty. The files read and the tool calls come
/// from the tool session, never from the model's text.
/// </summary>
public sealed record ProposalContent(
    string? Summary,
    IReadOnlyList<ProposedChangeContent>? Changes,
    IReadOnlyList<string>? Risks,
    IReadOnlyList<string>? TestIdeas,
    IReadOnlyList<string>? OpenQuestions,
    IReadOnlyList<string>? SampleFilesRead = null,
    IReadOnlyList<ToolCallRecord>? ToolCalls = null);

/// <summary>Content that passed the shape rules, with typed edits. Not yet verified against the sample.</summary>
public sealed record ValidatedProposal(
    string Summary,
    IReadOnlyList<ProposedChange> Changes,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> TestIdeas,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> SampleFilesRead,
    IReadOnlyList<ToolCallRecord> ToolCalls);

public sealed record ProposalValidation(
    ValidatedProposal? Value, IReadOnlyList<ValidationError> Errors, ProposalRejection? Rejection);

/// <summary>Operator-set ceilings: most changes, and most bytes of quoted plus replacement text.</summary>
public sealed record ProposalLimits(int MaxChanges, int MaxTotalBytes)
{
    public const int DefaultMaxChanges = 5;
    public const int DefaultMaxTotalBytes = 6000;

    public static ProposalLimits Defaults { get; } = new(DefaultMaxChanges, DefaultMaxTotalBytes);
}
