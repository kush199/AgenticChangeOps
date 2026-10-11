namespace AgenticChangeOps.Intake.Models;

public sealed class QueryResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private QueryResult(bool isSuccess, T? value, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static QueryResult<T> Success(T? value) =>
        new(true, value, Array.Empty<ValidationError>());

    public static QueryResult<T> Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, default, errors);
}
