using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Requirement;

/// <summary>
/// Raw output of the content generator, before validation. A null member means the section
/// was absent; an empty list means the section was present but empty.
/// </summary>
public sealed record DraftContent(
    string? Summary,
    IReadOnlyList<string>? FunctionalRequirements,
    IReadOnlyList<string>? AcceptanceCriteria,
    IReadOnlyList<string>? Assumptions,
    IReadOnlyList<string>? OpenQuestions);
