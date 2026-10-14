using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Workflow;

public interface IWorkflowService
{
    QueryResult<WorkflowStatus> GetStatus(Guid changeRequestId);
    Task<AdvanceResult> AdvanceAsync(Guid changeRequestId, CancellationToken cancellationToken = default);
}
