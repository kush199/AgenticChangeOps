namespace AgenticChangeOps.Intake.Models;

public sealed class DecisionResult
{
    public bool IsSuccess { get; }
    public ChangeRequest? ChangeRequest { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private DecisionResult(bool isSuccess, ChangeRequest? changeRequest, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        ChangeRequest = changeRequest;
        Errors = errors;
    }

    public static DecisionResult Success(ChangeRequest changeRequest) =>
        new(true, changeRequest, Array.Empty<ValidationError>());

    public static DecisionResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors);
}
