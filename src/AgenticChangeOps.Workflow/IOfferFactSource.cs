namespace AgenticChangeOps.Workflow;

/// <summary>
/// The recorded change proposals and reviews of a change request, reduced to the facts the offer rule
/// needs, so the workflow does not reference the agents that own those records.
/// </summary>
public interface IOfferFactSource
{
    IReadOnlyList<ProposalFact> GetProposals(Guid changeRequestId);

    IReadOnlyList<ReviewFact> GetReviews(Guid changeRequestId);
}
