using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Drafting;

/// <summary>
/// What an agent decides about generated content: usable, unusable (with errors), or refused with
/// a typed reason.
/// </summary>
public sealed class ReviewOutcome<TContent, TRejection>
    where TContent : class
    where TRejection : struct, Enum
{
    private ReviewOutcome(TContent? content, IReadOnlyList<ValidationError> errors, TRejection? rejection)
    {
        Content = content;
        Errors = errors;
        Rejection = rejection;
    }

    public TContent? Content { get; }
    public IReadOnlyList<ValidationError> Errors { get; }
    public TRejection? Rejection { get; }

    public static ReviewOutcome<TContent, TRejection> Valid(TContent content) =>
        new(content, Array.Empty<ValidationError>(), null);

    public static ReviewOutcome<TContent, TRejection> Invalid(IReadOnlyList<ValidationError> errors) =>
        new(null, errors, null);

    public static ReviewOutcome<TContent, TRejection> Rejected(TRejection reason) =>
        new(null, Array.Empty<ValidationError>(), reason);
}
