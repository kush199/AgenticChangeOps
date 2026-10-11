using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Models;

public sealed record ApprovalDecisionRequest(
    Guid ChangeRequestId,
    string ApproverName,
    ApprovalDecision Decision,
    string? Reason);
