namespace AgenticChangeOps.Intake.Models;

public sealed class SubmissionResult
{
    public bool IsSuccess { get; }
    public ChangeRequest? ChangeRequest { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private SubmissionResult(bool isSuccess, ChangeRequest? changeRequest, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        ChangeRequest = changeRequest;
        Errors = errors;
    }

    public static SubmissionResult Success(ChangeRequest changeRequest) =>
        new(true, changeRequest, Array.Empty<ValidationError>());

    public static SubmissionResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors);
}
