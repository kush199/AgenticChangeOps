using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Drafting;

/// <summary>
/// What the engine returns: the stored record, or errors (with the generation failure category
/// when generation or content failed), or a typed rejection. Each agent maps it to its own
/// public result type.
/// </summary>
public sealed class DraftingOutcome<TRecord, TRejection>
    where TRecord : class
    where TRejection : struct, Enum
{
    private DraftingOutcome(
        TRecord? record,
        IReadOnlyList<ValidationError> errors,
        GenerationFailureCategory? failureCategory,
        TRejection? rejection)
    {
        Record = record;
        Errors = errors;
        FailureCategory = failureCategory;
        Rejection = rejection;
    }

    public bool IsSuccess => Record is not null;
    public TRecord? Record { get; }
    public IReadOnlyList<ValidationError> Errors { get; }
    public GenerationFailureCategory? FailureCategory { get; }
    public TRejection? Rejection { get; }

    public static DraftingOutcome<TRecord, TRejection> Success(TRecord record) =>
        new(record, Array.Empty<ValidationError>(), null, null);

    public static DraftingOutcome<TRecord, TRejection> Failure(
        IReadOnlyList<ValidationError> errors, GenerationFailureCategory? category = null) =>
        new(null, errors, category, null);

    public static DraftingOutcome<TRecord, TRejection> Rejected(TRejection reason) =>
        new(null, Array.Empty<ValidationError>(), null, reason);
}
