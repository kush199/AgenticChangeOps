using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>
/// Slice 021, US4: the offered review is information only. Neither the manual advance nor the
/// automatic path ever runs it, and a request whose only offer is the review has nothing due.
/// </summary>
public class ReviewOfferAdvanceTests
{
    private sealed class ProposalsOnly : IProposalOffer
    {
        public bool IsOffered => true;
    }

    private static async Task<(WorkflowTestFixture F, Guid Id, WorkflowService Service)> ReviewOnlyOfferAsync()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));
        return (f, id, f.WorkflowWithOffers());
    }

    [Fact]
    public async Task Advance_WithTheReviewAsTheOnlyOffer_ReportsNothingDue_AndChangesNothing()
    {
        var (f, id, service) = await ReviewOnlyOfferAsync();
        var audit = f.Store.GetAuditEntries(id).Count;
        var designCalls = f.DesignGenerator.CallCount;
        var requirementCalls = f.RequirementsGenerator.CallCount;

        var result = await service.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.NothingDue, result.Outcome);
        Assert.Null(result.Step);
        Assert.Equal(WorkflowStep.Review, result.Status!.OfferedStep);
        Assert.Equal(audit, f.Store.GetAuditEntries(id).Count);
        Assert.Equal(designCalls, f.DesignGenerator.CallCount);
        Assert.Equal(requirementCalls, f.RequirementsGenerator.CallCount);
        Assert.Equal(1, f.DesignCount(id));
        Assert.Equal(1, f.DraftCount(id));
    }

    [Fact]
    public async Task TheAutomaticPath_WithTheReviewAsTheOnlyOffer_ReportsNothingDue_AndChangesNothing()
    {
        var (f, id, service) = await ReviewOnlyOfferAsync();
        var audit = f.Store.GetAuditEntries(id).Count;

        var result = await service.TryRunAsync(id);

        Assert.Equal(AdvanceOutcome.NothingDue, result.Outcome);
        Assert.Equal(audit, f.Store.GetAuditEntries(id).Count);
    }

    [Fact]
    public async Task TheReview_IsNeverANextStep_InAnyStatusOrFactCombination()
    {
        var f = new WorkflowTestFixture();
        var service = f.WorkflowWithOffers(new ProposalsOnly());
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));

        foreach (var status in Enum.GetValues<ChangeRequestStatus>())
        {
            var withDraft = await f.SubmittedWithDraftAsync();
            f.SeedStatus(withDraft, status);
            var noDraft = f.SubmitSynthetic();
            f.SeedStatus(noDraft, status);
            var designed = await f.DesignCompleteAsync();
            f.SeedStatus(designed, status);

            foreach (var id in new[] { withDraft, noDraft, designed })
            {
                var derived = service.GetStatus(id).Value!;
                Assert.NotEqual(WorkflowStep.Review, derived.NextStep);
                Assert.DoesNotContain(derived.CompletedSteps, c => c.Step == WorkflowStep.Review);
            }
        }
    }
}
