using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class OverlapTests
{
    [Fact]
    public async Task WhileAManualAdvanceHoldsTheRequest_TheAutomaticRunnerReportsBusy_AndStoresNothing()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        var probe = new GateProbe();
        probe.Install(f.Base.RequirementsGenerator);

        var manual = f.Workflow.AdvanceAsync(id);
        await probe.Entered;
        var automatic = await f.Workflow.TryRunAsync(id);
        var check = await f.Automatic.RunCheckAsync();

        Assert.Equal(AdvanceOutcome.Busy, automatic.Outcome);
        Assert.Equal(0, check.Ran);
        Assert.Equal(0, check.Failed);
        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);

        probe.Release();
        var manualResult = await manual;

        Assert.Equal(AdvanceOutcome.Advanced, manualResult.Outcome);
        Assert.Equal(1, f.Base.DraftCount(id));
    }

    [Fact]
    public async Task AManualAdvanceStartedWhileAnAutomaticStepIsInFlight_WaitsThenFindsNothingDue()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        var probe = new GateProbe();
        probe.Install(f.Base.RequirementsGenerator);

        var check = f.Automatic.RunCheckAsync();
        await probe.Entered;
        var manual = f.Workflow.AdvanceAsync(id);
        probe.Release();
        var summary = await check;
        var manualResult = await manual;

        Assert.Equal(1, summary.Ran);
        Assert.Equal(AdvanceOutcome.NothingDue, manualResult.Outcome);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);
    }

    [Fact]
    public async Task TwoOverlappingChecks_ProduceExactlyOneNewDraft()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        var probe = new GateProbe();
        probe.Install(f.Base.RequirementsGenerator);

        var first = f.Automatic.RunCheckAsync();
        await probe.Entered;
        var second = await f.Automatic.RunCheckAsync();
        probe.Release();
        var firstSummary = await first;

        Assert.Equal(0, second.Ran);
        Assert.Equal(0, second.Failed);
        Assert.Equal(1, firstSummary.Ran);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);
    }

    [Fact]
    public async Task AfterAStepFails_TheRequestIsNotLeftBusy()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.RequirementsFail(GenerationFailureCategory.TimedOut);
        await f.Automatic.RunCheckAsync();
        f.RequirementsSucceed();

        var result = await f.Workflow.TryRunAsync(id);

        Assert.NotEqual(AdvanceOutcome.Busy, result.Outcome);
        Assert.Equal(AdvanceOutcome.Advanced, result.Outcome);
    }

    [Fact]
    public async Task AStepCancelledMidCall_StoresNothing_AndIsSimplyDueAgain()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        using var cts = new CancellationTokenSource();
        var probe = new GateProbe();
        probe.Install(f.Base.RequirementsGenerator);
        var auditBefore = f.Base.AuditTypes(id);

        var check = f.Automatic.RunCheckAsync(cts.Token);
        await probe.Entered;
        cts.Cancel();
        try { await check; } catch (OperationCanceledException) { }

        Assert.Equal(0, f.Base.DraftCount(id));
        Assert.Equal(auditBefore, f.Base.AuditTypes(id));
        Assert.Null(f.Store.GetAutomaticAttempt(id));

        f.RequirementsSucceed();
        var next = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, next.Ran);
        Assert.Equal(1, f.Base.DraftCount(id));
    }
}
