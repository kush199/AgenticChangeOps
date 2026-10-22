using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer;

public sealed class ReviewResult
{
    public bool IsSuccess { get; }
    public Review? Review { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>Set only when generation failed or its content was unusable.</summary>
    public GenerationFailureCategory? FailureCategory { get; }

    /// <summary>Set only when the system refused what the model produced.</summary>
    public ReviewRejection? Rejection { get; }

    private ReviewResult(
        bool isSuccess,
        Review? review,
        IReadOnlyList<ValidationError> errors,
        GenerationFailureCategory? failureCategory,
        ReviewRejection? rejection)
    {
        IsSuccess = isSuccess;
        Review = review;
        Errors = errors;
        FailureCategory = failureCategory;
        Rejection = rejection;
    }

    public static ReviewResult Success(Review review) =>
        new(true, review, Array.Empty<ValidationError>(), null, null);

    public static ReviewResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors, null, null);

    public static ReviewResult Failure(IReadOnlyList<ValidationError> errors, GenerationFailureCategory category) =>
        new(false, null, errors, category, null);

    /// <summary>A fixed message naming the reason only: never finding text, a reference or any model text.</summary>
    public static ReviewResult Rejected(ReviewRejection rejection) =>
        new(false, null, new[] { new ValidationError("Review", $"The review was rejected ({rejection}).") }, null, rejection);
}
