using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

/// <summary>
/// Slice 021, US4/AC2: automatic advancement never runs, records or schedules the review, even when
/// the status offers it.
/// </summary>
public class ReviewNotAutomaticTests
{
    [Fact]
    public async Task AutomaticChecks_WithTheReviewOffered_RunNothing_AndAddNoAuditEntry()
    {
        var f = new WorkflowTestFixture();
        var settings = new AutomaticAdvancementSettings(
            Enabled: true,
            Interval: TimeSpan.FromMinutes(5),
            MaxStepsPerCheck: 5,
            FailureWait: TimeSpan.FromSeconds(900),
            MaxConsecutiveFailures: 3);
        var source = new AutomaticStatusSource(f.Store, settings);
        var workflow = new WorkflowService(
            f.Store, f.Requirements, f.Designs, f.Availability, source, null, f.ReviewOffer, f.OfferFacts);
        var automatic = new AutomaticAdvancementService(f.Store, workflow, workflow, f.Clock, settings, f.Availability);

        var id = await f.ApprovedWithDraftAsync();
        await workflow.AdvanceAsync(id);   // the design, by a person
        f.OfferFacts.Proposals.Add(new ProposalFact(1, 1));
        Assert.Equal(WorkflowStep.Review, workflow.GetStatus(id).Value!.OfferedStep);
        var audit = f.Store.GetAuditEntries(id).Count;
        var designCalls = f.DesignGenerator.CallCount;
        var requirementCalls = f.RequirementsGenerator.CallCount;

        f.Clock.Now += TimeSpan.FromHours(1);
        var first = await automatic.RunCheckAsync();
        f.Clock.Now += TimeSpan.FromHours(1);
        var second = await automatic.RunCheckAsync();

        Assert.Equal(0, first.Ran);
        Assert.Equal(0, first.Failed);
        Assert.Equal(0, second.Ran);
        Assert.Equal(audit, f.Store.GetAuditEntries(id).Count);
        Assert.DoesNotContain(f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.ReviewGenerated);
        Assert.Equal(designCalls, f.DesignGenerator.CallCount);
        Assert.Equal(requirementCalls, f.RequirementsGenerator.CallCount);
        Assert.Equal(WorkflowStep.Review, workflow.GetStatus(id).Value!.OfferedStep);
    }
}
