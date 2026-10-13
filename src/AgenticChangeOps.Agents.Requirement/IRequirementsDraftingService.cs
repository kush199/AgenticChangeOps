using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Services;

public interface IRequirementsDraftingService
{
    Task<DraftingResult> DraftAsync(Guid changeRequestId, CancellationToken cancellationToken = default);
    QueryResult<RequirementsDraft?> GetLatestDraft(Guid changeRequestId);
    QueryResult<IReadOnlyList<RequirementsDraft>> GetDraftHistory(Guid changeRequestId);
}
