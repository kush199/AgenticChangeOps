namespace AgenticChangeOps.Intake.Models;

public sealed class DraftingResult
{
    public bool IsSuccess { get; }
    public RequirementsDraft? Draft { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private DraftingResult(bool isSuccess, RequirementsDraft? draft, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        Draft = draft;
        Errors = errors;
    }

    public static DraftingResult Success(RequirementsDraft draft) =>
        new(true, draft, Array.Empty<ValidationError>());

    public static DraftingResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors);
}
