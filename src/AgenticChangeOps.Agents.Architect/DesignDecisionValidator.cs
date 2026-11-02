using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Field rules for a design decision: the same limits and trimming as the approval decision. Messages
/// never carry what the caller sent.
/// </summary>
public sealed class DesignDecisionValidator
{
    private const int ApproverNameMaxLength = 100;
    private const int ReasonMaxLength = 1000;

    public IReadOnlyList<ValidationError> Validate(string? approverName, DesignVerdict verdict, string? reason)
    {
        var errors = new List<ValidationError>();

        var name = approverName?.Trim();
        if (string.IsNullOrEmpty(name))
            errors.Add(new ValidationError("ApproverName", "ApproverName is required."));
        else if (name.Length > ApproverNameMaxLength)
            errors.Add(new ValidationError("ApproverName",
                $"ApproverName must not exceed {ApproverNameMaxLength} characters."));

        var verdictIsDefined = Enum.IsDefined(verdict);
        if (!verdictIsDefined)
            errors.Add(new ValidationError("Decision",
                "Decision is not a recognised value. Accepted values: Accept, Reject."));

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
        {
            if (verdictIsDefined && verdict == DesignVerdict.Reject)
                errors.Add(new ValidationError("Reason", "Reason is required when the decision is Reject."));
        }
        else if (trimmedReason.Length > ReasonMaxLength)
        {
            errors.Add(new ValidationError("Reason", $"Reason must not exceed {ReasonMaxLength} characters."));
        }

        return errors.AsReadOnly();
    }
}
