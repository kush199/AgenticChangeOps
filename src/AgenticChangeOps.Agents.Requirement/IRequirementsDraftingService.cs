using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Requirement;

public interface IRequirementsDraftingService
{
    Task<DraftingResult> DraftAsync(
        Guid changeRequestId, CancellationToken cancellationToken = default, bool automatic = false);
    QueryResult<RequirementsDraft?> GetLatestDraft(Guid changeRequestId);
    QueryResult<IReadOnlyList<RequirementsDraft>> GetDraftHistory(Guid changeRequestId);
}
