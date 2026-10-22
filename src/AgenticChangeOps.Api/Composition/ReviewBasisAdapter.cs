using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Agents.Reviewer;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Gives the Reviewer the latest requirements draft, design draft and proposal without the Reviewer
/// referencing the other agents: reads them through their own services and copies the sections into
/// the Reviewer's own read model.
/// </summary>
public sealed class ReviewBasisAdapter : IReviewBasisSource
{
    private readonly IRequirementsDraftingService _requirements;
    private readonly IDesignDraftingService _designs;
    private readonly IProposalService _proposals;

    public ReviewBasisAdapter(
        IRequirementsDraftingService requirements, IDesignDraftingService designs, IProposalService proposals)
    {
        _requirements = requirements;
        _designs = designs;
        _proposals = proposals;
    }

    public ReviewBases GetLatest(Guid changeRequestId)
    {
        ReviewRequirementsBasis? requirements = null;
        var drafts = _requirements.GetLatestDraft(changeRequestId);
        if (drafts.IsSuccess && drafts.Value is { } draft)
            requirements = new ReviewRequirementsBasis(
                draft.Version, draft.Summary, draft.FunctionalRequirements,
                draft.AcceptanceCriteria, draft.Assumptions, draft.OpenQuestions);

        ReviewDesignBasis? design = null;
        var designs = _designs.GetLatestDesign(changeRequestId);
        if (designs.IsSuccess && designs.Value is { } latestDesign)
            design = new ReviewDesignBasis(
                latestDesign.Version, latestDesign.RequirementsVersion, latestDesign.ApproachSummary,
                latestDesign.AffectedComponents, latestDesign.ImpactAndRisks, latestDesign.Dependencies,
                latestDesign.RollbackConsiderations, latestDesign.OpenQuestions);

        ReviewProposalBasis? proposal = null;
        var proposals = _proposals.GetLatestProposal(changeRequestId);
        if (proposals.IsSuccess && proposals.Value is { } latestProposal)
            proposal = new ReviewProposalBasis(
                latestProposal.Version, latestProposal.RequirementsVersion, latestProposal.DesignVersion,
                latestProposal.Summary,
                latestProposal.Changes
                    .Select(change => new ReviewProposedChange(
                        change.Path,
                        change.Kind == ProposalChangeKind.Add ? ReviewChangeKind.Add : ReviewChangeKind.Change,
                        change.ExistingText,
                        change.ReplacementText))
                    .ToArray(),
                latestProposal.Risks, latestProposal.TestIdeas, latestProposal.OpenQuestions);

        return new ReviewBases(requirements, design, proposal);
    }
}
