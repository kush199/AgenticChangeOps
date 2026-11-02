using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>
/// Slice 021, US1/US2/US3: the workflow status offers the review once a current proposal exists. The
/// offer is an extra, optional member; the stage, next step, reason, completed steps and automatic
/// block are exactly what they were.
/// </summary>
public class ReviewOfferTests
{
    private sealed class ProposalsOn : IProposalOffer
    {
        public bool IsOffered => true;
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ACurrentProposalWithNoReview_OffersTheReview_AndChangesNothingElse()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        var plain = f.Workflow.GetStatus(id).Value!;
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));

        var offered = f.WorkflowWithOffers().GetStatus(id).Value!;

        Assert.Equal(WorkflowStep.Review, offered.OfferedStep);
        Assert.Equal(plain.Stage, offered.Stage);
        Assert.Equal(plain.NextStep, offered.NextStep);
        Assert.Equal(plain.Reason, offered.Reason);
        Assert.Equal(plain.CompletedSteps, offered.CompletedSteps);
        Assert.Equal(plain.Automatic, offered.Automatic);
        Assert.Contains("\"offeredStep\":\"Review\"", JsonSerializer.Serialize(offered, Web));
    }

    [Fact]
    public async Task TheReview_ReplacesTheProposalOffer_WhereACurrentProposalExists()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        var service = f.WorkflowWithOffers(new ProposalsOn());

        Assert.Equal(WorkflowStep.Proposal, service.GetStatus(id).Value!.OfferedStep);

        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));
        Assert.Equal(WorkflowStep.Review, service.GetStatus(id).Value!.OfferedStep);

        f.OfferFacts.Reviews.Add(new ReviewFact(1));
        Assert.Null(service.GetStatus(id).Value!.OfferedStep);
    }

    [Fact]
    public async Task WithReviewingNotAvailable_TheStatusStillWorks_AndShowsNoReviewOffer()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));
        f.ReviewOffer.Available = false;

        var status = f.WorkflowWithOffers().GetStatus(id);

        Assert.True(status.IsSuccess);
        Assert.Null(status.Value!.OfferedStep);
        Assert.DoesNotContain("offeredStep", JsonSerializer.Serialize(status.Value, Web));
    }

    [Fact]
    public async Task WithNoOfferSeams_TheStatusIsExactlyToday()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();

        var plain = f.Workflow.GetStatus(id).Value!;

        Assert.Null(plain.OfferedStep);
        Assert.Equal(JsonSerializer.Serialize(plain, Web), JsonSerializer.Serialize(f.Workflow.GetStatus(id).Value!, Web));
    }

    [Fact]
    public async Task WithTheProposalOfferButNoFactSource_TheProposalOfferIsExactlyToday()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        var service = new WorkflowService(f.Store, f.Requirements, f.Designs, f.Availability, null, new ProposalsOn());

        Assert.Equal(WorkflowStep.Proposal, service.GetStatus(id).Value!.OfferedStep);
    }

    [Fact]
    public async Task AProposalOnAnOlderDesign_DoesNotTriggerTheReview_AndAReviewOfAnOlderProposalDoesNotSuppressIt()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        var service = f.WorkflowWithOffers();

        // Only proposal 1 exists, based on design 7: not the latest design (1).
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 7));
        Assert.Null(service.GetStatus(id).Value!.OfferedStep);

        // A newer current proposal exists; the review is of the older proposal only.
        f.OfferFacts.Proposals.Add(new ProposalFact(2, 1));
        f.OfferFacts.Reviews.Add(new ReviewFact(1));
        Assert.Equal(WorkflowStep.Review, service.GetStatus(id).Value!.OfferedStep);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Deferred)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Submitted)]
    public async Task ARequestThatIsNotApproved_NeverShowsTheReview(ChangeRequestStatus status)
    {
        var f = new WorkflowTestFixture();
        var id = await f.SubmittedWithDraftAsync();
        f.SeedStatus(id, status);
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));

        Assert.Null(f.WorkflowWithOffers().GetStatus(id).Value!.OfferedStep);
    }

    [Fact]
    public void AnUnknownId_BehavesAsBefore()
    {
        var f = new WorkflowTestFixture();
        var missing = Guid.NewGuid();

        var before = f.Workflow.GetStatus(missing);
        var after = f.WorkflowWithOffers().GetStatus(missing);

        Assert.False(after.IsSuccess);
        Assert.Equal(before.Errors.Select(e => (e.FieldName, e.Message)), after.Errors.Select(e => (e.FieldName, e.Message)));
        Assert.Equal(0, f.OfferFacts.Reads);
    }

    [Fact]
    public async Task ReadingTheStatus_IsRepeatable_AndWritesNothing()
    {
        var f = new WorkflowTestFixture();
        var id = await f.DesignCompleteAsync();
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));
        var service = f.WorkflowWithOffers();
        var audit = f.Store.GetAuditEntries(id).Count;
        var drafts = f.DraftCount(id);
        var designs = f.DesignCount(id);
        var statusBefore = f.Store.FindById(id)!.Status;

        var first = service.GetStatus(id).Value!;
        var second = service.GetStatus(id).Value!;

        Assert.Equal(first, second);
        Assert.Equal(audit, f.Store.GetAuditEntries(id).Count);
        Assert.Equal(drafts, f.DraftCount(id));
        Assert.Equal(designs, f.DesignCount(id));
        Assert.Equal(statusBefore, f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task TheReviewingSeam_IsOnlyAsked_WhenACurrentProposalHasNoReview()
    {
        var f = new WorkflowTestFixture();
        var service = f.WorkflowWithOffers();

        var early = await f.ApprovedWithDraftAsync();           // ReadyForDesign
        service.GetStatus(early);
        Assert.Equal(0, f.ReviewOffer.Reads);

        var id = await f.DesignCompleteAsync();                 // no proposal yet
        service.GetStatus(id);
        Assert.Equal(0, f.ReviewOffer.Reads);

        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));
        f.OfferFacts.Reviews.Add(new ReviewFact(1));            // already reviewed
        service.GetStatus(id);
        Assert.Equal(0, f.ReviewOffer.Reads);

        f.OfferFacts.Reviews.Clear();
        service.GetStatus(id);
        Assert.Equal(1, f.ReviewOffer.Reads);
    }
}
