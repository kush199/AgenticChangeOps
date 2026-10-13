using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Requirement;

public sealed class DraftingResult
{
    public bool IsSuccess { get; }
    public RequirementsDraft? Draft { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Set only when drafting failed because generation failed or its content was unusable.
    /// Null for success and for eligibility failures.
    /// </summary>
    public GenerationFailureCategory? FailureCategory { get; }

    private DraftingResult(
        bool isSuccess,
        RequirementsDraft? draft,
        IReadOnlyList<ValidationError> errors,
        GenerationFailureCategory? failureCategory = null)
    {
        IsSuccess = isSuccess;
        Draft = draft;
        Errors = errors;
        FailureCategory = failureCategory;
    }

    public static DraftingResult Success(RequirementsDraft draft) =>
        new(true, draft, Array.Empty<ValidationError>());

    public static DraftingResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors);

    public static DraftingResult Failure(IReadOnlyList<ValidationError> errors, GenerationFailureCategory category) =>
        new(false, null, errors, category);
}
