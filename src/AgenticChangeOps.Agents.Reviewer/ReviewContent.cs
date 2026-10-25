using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>One finding as the model wrote it, before validation. Category and severity are raw words.</summary>
public sealed record ReviewFindingContent(
    string? Category,
    string? Severity,
    string? Statement,
    IReadOnlyList<string>? References);

/// <summary>
/// Raw output of the content generator, before validation. A null member means the section was
/// absent; an empty list means it was present but empty. It has no place for a system check:
/// whatever else the model writes is never part of it.
/// </summary>
public sealed record ReviewContent(
    IReadOnlyList<ReviewFindingContent>? Findings,
    IReadOnlyList<string>? OpenQuestions);

/// <summary>Content that passed every rule, with typed findings and references that resolve.</summary>
public sealed record ValidatedReview(
    IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<string> OpenQuestions);

/// <summary>Exactly one of the three is set: a value, shape errors, or a typed rejection.</summary>
public sealed record ReviewValidation(
    ValidatedReview? Value,
    IReadOnlyList<ValidationError> Errors,
    ReviewRejection? Rejection);
