using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Services;

public sealed class ApprovalDecisionValidator
{
    private const int ApproverNameMaxLength = 100;
    private const int ReasonMaxLength = 1000;

    public IReadOnlyList<ValidationError> Validate(ApprovalDecisionRequest request, ChangeRequest? existing)
    {
        var errors = new List<ValidationError>();

        if (existing is null)
        {
            errors.Add(new ValidationError("ChangeRequestId",
                $"Change request '{request.ChangeRequestId}' was not found."));
        }

        ValidateApproverName(errors, request.ApproverName);
        var decisionIsDefined = ValidateDecision(errors, request.Decision);
        ValidateReason(errors, request, decisionIsDefined);

        if (existing is not null)
            ValidateStatus(errors, existing.Status);

        return errors.AsReadOnly();
    }

    private static void ValidateApproverName(List<ValidationError> errors, string? approverName)
    {
        var trimmed = approverName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new ValidationError("ApproverName", "ApproverName is required."));
            return;
        }
        if (trimmed.Length > ApproverNameMaxLength)
            errors.Add(new ValidationError("ApproverName",
                $"ApproverName must not exceed {ApproverNameMaxLength} characters."));
    }

    private static bool ValidateDecision(List<ValidationError> errors, ApprovalDecision decision)
    {
        if (Enum.IsDefined(decision))
            return true;

        errors.Add(new ValidationError("Decision",
            "Decision is not a recognised value. Accepted values: Approve, Reject, Defer."));
        return false;
    }

    private static void ValidateReason(
        List<ValidationError> errors, ApprovalDecisionRequest request, bool decisionIsDefined)
    {
        var trimmed = request.Reason?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            if (decisionIsDefined && request.Decision is ApprovalDecision.Reject or ApprovalDecision.Defer)
                errors.Add(new ValidationError("Reason",
                    $"Reason is required when the decision is {request.Decision}."));
            return;
        }

        if (trimmed.Length > ReasonMaxLength)
            errors.Add(new ValidationError("Reason",
                $"Reason must not exceed {ReasonMaxLength} characters."));
    }

    private static void ValidateStatus(List<ValidationError> errors, ChangeRequestStatus status)
    {
        switch (status)
        {
            case ChangeRequestStatus.Submitted:
            case ChangeRequestStatus.Deferred:
                return;
            case ChangeRequestStatus.Approved:
            case ChangeRequestStatus.Rejected:
                errors.Add(new ValidationError("Status",
                    $"Change request is already {status} and in a final state; " +
                    "no further decisions are allowed."));
                return;
            default:
                errors.Add(new ValidationError("Status",
                    $"Change request in status '{status}' is not eligible for a decision."));
                return;
        }
    }
}
