using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.Configuration;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Gives the workflow the recorded proposals and reviews as reduced facts, without the workflow
/// referencing the Developer or Reviewer agents. Reads through the shared store only.
/// </summary>
public sealed class OfferFactSource : IOfferFactSource
{
    private readonly IChangeRequestStore _store;

    public OfferFactSource(IChangeRequestStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ProposalFact> GetProposals(Guid changeRequestId) =>
        _store.GetAgentOutputs<ChangeProposal>(changeRequestId)
            .Select(p => new ProposalFact(p.Version, p.DesignVersion))
            .ToArray();

    public IReadOnlyList<ReviewFact> GetReviews(Guid changeRequestId) =>
        _store.GetAgentOutputs<Review>(changeRequestId)
            .Select(r => new ReviewFact(r.ProposalVersion))
            .ToArray();
}

/// <summary>
/// Tells the workflow whether to offer the review. Only ever shown, never run by the workflow. It
/// checks the model and review settings itself, on each ask, and logs nothing: reading the status
/// must not build the review setup or write the "not configured" line a review request writes.
/// </summary>
public sealed class ReviewOffer : IReviewOffer
{
    private readonly IConfiguration _configuration;

    public ReviewOffer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsOffered
    {
        get
        {
            try
            {
                _ = AzureOpenAIGeneratorOptions.FromEnvironment(name => _configuration[name]);
                _ = ReviewSettings.FromConfiguration(name => _configuration[name]);
                return true;
            }
            catch (GeneratorConfigurationException)
            {
                return false;
            }
        }
    }
}
