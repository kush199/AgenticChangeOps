using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

/// <summary>
/// Slice 016, FR-020 and SC-008: automatic advancement never runs, records or schedules the
/// proposal, whether or not it is offered.
/// </summary>
public class ProposalNotAutomaticTests
{
    private sealed class Offer : IProposalOffer
    {
        public bool IsOffered { get; init; }
    }

    private sealed class Rig
    {
        public Rig(bool offered)
        {
            Base = new WorkflowTestFixture();
            var settings = new AutomaticAdvancementSettings(
                Enabled: true,
                Interval: TimeSpan.FromMinutes(5),
                MaxStepsPerCheck: 5,
                FailureWait: TimeSpan.FromSeconds(900),
                MaxConsecutiveFailures: 3);
            var source = new AutomaticStatusSource(Base.Store, settings);
            Workflow = new WorkflowService(
                Base.Store, Base.Requirements, Base.Designs, Base.Availability, source,
                new Offer { IsOffered = offered });
            Automatic = new AutomaticAdvancementService(
                Base.Store, Workflow, Workflow, Base.Clock, settings, Base.Availability);
        }

        public WorkflowTestFixture Base { get; }
        public WorkflowService Workflow { get; }
        public AutomaticAdvancementService Automatic { get; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticChecks_AtDesignComplete_MakeNoProposal_AndAddNoAuditEntry(bool offered)
    {
        var rig = new Rig(offered);
        var id = await rig.Base.ApprovedWithDraftAsync();
        await rig.Workflow.AdvanceAsync(id);   // the design, by a person
        var audit = rig.Base.Store.GetAuditEntries(id).Count;

        rig.Base.Clock.Now += TimeSpan.FromHours(1);
        var first = await rig.Automatic.RunCheckAsync();
        rig.Base.Clock.Now += TimeSpan.FromHours(1);
        var second = await rig.Automatic.RunCheckAsync();

        Assert.Equal(0, first.Ran);
        Assert.Equal(0, first.Failed);
        Assert.Equal(0, second.Ran);
        Assert.Equal(audit, rig.Base.Store.GetAuditEntries(id).Count);
        Assert.DoesNotContain(rig.Base.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.ProposalGenerated);
        Assert.DoesNotContain(rig.Base.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.ToolCalled);
        Assert.Null(rig.Workflow.GetStatus(id).Value!.NextStep);
    }

    [Fact]
    public async Task TheRunnerEntryPoint_AtDesignComplete_ReportsNothingDue_WhenTheProposalIsOffered()
    {
        var rig = new Rig(offered: true);
        var id = await rig.Base.ApprovedWithDraftAsync();
        await rig.Workflow.AdvanceAsync(id);

        var result = await rig.Workflow.TryRunAsync(id);

        Assert.Equal(AdvanceOutcome.NothingDue, result.Outcome);
        Assert.Null(result.Step);
    }
}
