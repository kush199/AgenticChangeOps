using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Services;

public interface IApprovalService
{
    DecisionResult RecordDecision(ApprovalDecisionRequest request);
    bool IsClearedToProceed(Guid changeRequestId);
}
