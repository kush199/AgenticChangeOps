using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

public sealed class DesignDraftingResult
{
    public bool IsSuccess { get; }
    public DesignDraft? Draft { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Set only when designing failed because generation failed or its content was unusable.
    /// Null for success and for eligibility failures.
    /// </summary>
    public GenerationFailureCategory? FailureCategory { get; }

    private DesignDraftingResult(
        bool isSuccess,
        DesignDraft? draft,
        IReadOnlyList<ValidationError> errors,
        GenerationFailureCategory? failureCategory = null)
    {
        IsSuccess = isSuccess;
        Draft = draft;
        Errors = errors;
        FailureCategory = failureCategory;
    }

    public static DesignDraftingResult Success(DesignDraft draft) =>
        new(true, draft, Array.Empty<ValidationError>());

    public static DesignDraftingResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors);

    public static DesignDraftingResult Failure(IReadOnlyList<ValidationError> errors, GenerationFailureCategory category) =>
        new(false, null, errors, category);
}
