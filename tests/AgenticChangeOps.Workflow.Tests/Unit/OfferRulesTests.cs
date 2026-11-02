namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>
/// Slice 021: which step the workflow status offers, as a pure function of recorded facts
/// (the table in specs/021-workflow-offers-review/data-model.md).
/// </summary>
public class OfferRulesTests
{
    private static readonly ProposalFact[] None = Array.Empty<ProposalFact>();
    private static readonly ReviewFact[] NoReviews = Array.Empty<ReviewFact>();

    private static WorkflowStep? Offer(
        WorkflowStage stage = WorkflowStage.DesignComplete,
        int? design = 1,
        bool proposals = true,
        bool reviewing = true,
        ProposalFact[]? proposalFacts = null,
        ReviewFact[]? reviewFacts = null) =>
        OfferRules.Derive(stage, design, proposals, () => reviewing, proposalFacts ?? None, reviewFacts ?? NoReviews);

    [Theory]
    [InlineData(WorkflowStage.AwaitingRequirementsDraft)]
    [InlineData(WorkflowStage.AwaitingDecision)]
    [InlineData(WorkflowStage.ReadyForDesign)]
    [InlineData(WorkflowStage.Deferred)]
    [InlineData(WorkflowStage.Rejected)]
    public void OutsideDesignComplete_NothingIsOffered(WorkflowStage stage)
    {
        Assert.Null(Offer(stage, proposalFacts: new[] { new ProposalFact(1, 1) }));
        Assert.Null(Offer(stage));
    }

    [Fact]
    public void WithNoCurrentProposal_TheProposalIsOffered_WhenProposalsAreAvailable()
    {
        Assert.Equal(WorkflowStep.Proposal, Offer());
    }

    [Fact]
    public void WithNoCurrentProposal_NothingIsOffered_WhenProposalsAreNotAvailable()
    {
        Assert.Null(Offer(proposals: false));
    }

    [Fact]
    public void WithACurrentProposalAndNoReview_TheReviewIsOffered_WhenReviewingIsAvailable()
    {
        Assert.Equal(WorkflowStep.Review, Offer(proposalFacts: new[] { new ProposalFact(1, 1) }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithACurrentProposal_TheReviewReplacesTheProposal_WhateverProposalAvailabilityIs(bool proposals)
    {
        Assert.Equal(WorkflowStep.Review, Offer(proposals: proposals, proposalFacts: new[] { new ProposalFact(1, 1) }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithACurrentProposalAndReviewingNotAvailable_NothingIsOffered_NotTheProposal(bool proposals)
    {
        Assert.Null(Offer(proposals: proposals, reviewing: false, proposalFacts: new[] { new ProposalFact(1, 1) }));
    }

    [Fact]
    public void WithAReviewOfTheCurrentProposal_NothingFurtherIsOffered()
    {
        Assert.Null(Offer(
            proposalFacts: new[] { new ProposalFact(1, 1) },
            reviewFacts: new[] { new ReviewFact(1) }));
    }

    [Fact]
    public void AProposalOnlyOnAnOlderDesign_IsNotCurrent_SoItDoesNotTriggerTheReview()
    {
        // Design 2 is the latest; the only proposal is based on design 1.
        Assert.Equal(WorkflowStep.Proposal, Offer(design: 2, proposalFacts: new[] { new ProposalFact(1, 1) }));
        Assert.Null(Offer(design: 2, proposals: false, proposalFacts: new[] { new ProposalFact(1, 1) }));
    }

    [Fact]
    public void AReviewOfAnOlderProposal_DoesNotSuppressTheReviewOfANewerCurrentProposal()
    {
        var proposalFacts = new[] { new ProposalFact(1, 1), new ProposalFact(2, 2) };

        Assert.Equal(WorkflowStep.Review, Offer(design: 2, proposalFacts: proposalFacts, reviewFacts: new[] { new ReviewFact(1) }));
    }

    [Fact]
    public void AReviewOfTheNewerProposal_SuppressesTheOffer_EvenWithOlderOnesPresent()
    {
        var proposalFacts = new[] { new ProposalFact(1, 1), new ProposalFact(2, 2) };

        Assert.Null(Offer(design: 2, proposalFacts: proposalFacts, reviewFacts: new[] { new ReviewFact(1), new ReviewFact(2) }));
    }

    [Fact]
    public void SeveralProposalsOnTheLatestDesign_UseTheHighestVersion()
    {
        var proposalFacts = new[] { new ProposalFact(1, 1), new ProposalFact(2, 1) };

        Assert.Equal(WorkflowStep.Review, Offer(proposalFacts: proposalFacts, reviewFacts: new[] { new ReviewFact(1) }));
        Assert.Null(Offer(proposalFacts: proposalFacts, reviewFacts: new[] { new ReviewFact(2) }));
    }

    [Fact]
    public void TheOrderOfTheFacts_DoesNotChangeTheResult()
    {
        var proposalFacts = new[] { new ProposalFact(2, 1), new ProposalFact(1, 1) };
        var reviewFacts = new[] { new ReviewFact(1), new ReviewFact(2) };

        Assert.Equal(
            Offer(proposalFacts: proposalFacts, reviewFacts: reviewFacts),
            Offer(proposalFacts: proposalFacts.Reverse().ToArray(), reviewFacts: reviewFacts.Reverse().ToArray()));
        Assert.Null(Offer(proposalFacts: proposalFacts.Reverse().ToArray(), reviewFacts: reviewFacts));
    }

    [Fact]
    public void ReviewAvailability_IsOnlyAskedWhenACurrentProposalHasNoReview()
    {
        var asked = 0;
        bool Reviewing() { asked++; return true; }

        OfferRules.Derive(WorkflowStage.ReadyForDesign, 1, true, Reviewing, new[] { new ProposalFact(1, 1) }, NoReviews);
        OfferRules.Derive(WorkflowStage.DesignComplete, 1, true, Reviewing, None, NoReviews);
        OfferRules.Derive(WorkflowStage.DesignComplete, 1, true, Reviewing, new[] { new ProposalFact(1, 1) }, new[] { new ReviewFact(1) });
        Assert.Equal(0, asked);

        OfferRules.Derive(WorkflowStage.DesignComplete, 1, true, Reviewing, new[] { new ProposalFact(1, 1) }, NoReviews);
        Assert.Equal(1, asked);
    }
}
