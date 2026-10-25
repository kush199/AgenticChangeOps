using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Reviewer;

public interface IReviewService
{
    Task<ReviewResult> ReviewAsync(Guid changeRequestId, CancellationToken cancellationToken = default);

    QueryResult<Review?> GetLatestReview(Guid changeRequestId);

    QueryResult<IReadOnlyList<Review>> GetReviewHistory(Guid changeRequestId);
}
