using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Services;

public interface IChangeRequestService
{
    SubmissionResult Submit(SubmissionRequest request);
    ChangeRequest? GetById(Guid changeRequestId);
    IReadOnlyList<ChangeRequestAuditEntry>? GetAuditHistory(Guid changeRequestId);
}
