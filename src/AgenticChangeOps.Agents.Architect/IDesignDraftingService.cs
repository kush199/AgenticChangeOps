using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect;

public interface IDesignDraftingService
{
    Task<DesignDraftingResult> DesignAsync(Guid changeRequestId, CancellationToken cancellationToken = default);
    QueryResult<DesignDraft?> GetLatestDesign(Guid changeRequestId);
    QueryResult<IReadOnlyList<DesignDraft>> GetDesignHistory(Guid changeRequestId);
}
