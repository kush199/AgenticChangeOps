using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Services;

public sealed class ChangeRequestValidator
{
    public IReadOnlyList<ValidationError> Validate(SubmissionRequest request)
    {
        var errors = new List<ValidationError>();

        ValidateRequired(errors, request.Title?.Trim(), "Title", 200);
        ValidateRequired(errors, request.Description?.Trim(), "Description", 2000);
        ValidateRequired(errors, request.Requester?.Trim(), "Requester", 100);
        ValidateRequired(errors, request.AffectedSystem?.Trim(), "AffectedSystem", 200);
        ValidateEnum(errors, request.Priority, "Priority", "Low, Medium, High, Critical");
        ValidateEnum(errors, request.ChangeType, "ChangeType", "Feature, Bugfix, ConfigChange");

        return errors.AsReadOnly();
    }

    private static void ValidateRequired(
        List<ValidationError> errors, string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(fieldName, $"{fieldName} is required."));
            return;
        }
        if (value.Length > maxLength)
            errors.Add(new ValidationError(fieldName,
                $"{fieldName} must not exceed {maxLength} characters."));
    }

    private static void ValidateEnum<TEnum>(
        List<ValidationError> errors, TEnum value, string fieldName, string accepted)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            errors.Add(new ValidationError(fieldName,
                $"{fieldName} '{value}' is not valid. Accepted: {accepted}."));
    }
}
