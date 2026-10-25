using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>The Reviewer agent's view of the store's agent-neutral output point.</summary>
public static class ReviewStoreExtensions
{
    /// <summary>Stores a review and its audit entry together, or neither.</summary>
    public static void SaveReview(
        this IChangeRequestStore store, Review review, ChangeRequestAuditEntry auditEntry) =>
        store.SaveAgentOutput(review, auditEntry);

    /// <summary>Returns the reviews of a change request in ascending version order.</summary>
    public static IReadOnlyList<Review> GetReviews(this IChangeRequestStore store, Guid changeRequestId) =>
        store.GetAgentOutputs<Review>(changeRequestId);
}
