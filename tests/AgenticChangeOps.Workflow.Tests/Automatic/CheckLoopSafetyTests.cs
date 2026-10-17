using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class CheckLoopSafetyTests
{
    [Fact]
    public async Task AnUnexpectedErrorForOneRequest_CountsAsOneFailure_WritesNoRow_AndOthersAreStillServed()
    {
        var f = new AutomaticFixture(availability: new ThrowOnFirstCallAvailability());
        var first = f.Base.SubmitSynthetic();
        var second = f.Base.SubmitSynthetic();

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Ran);
        Assert.Empty(summary.FailuresByCategory);
        Assert.Null(f.Store.GetAutomaticAttempt(first));
        Assert.Equal(0, f.Base.DraftCount(first));
        Assert.Equal(1, f.Base.DraftCount(second));
    }

    [Fact]
    public async Task ACancelledCheck_StartsNoStep()
    {
        var f = new AutomaticFixture();
        var a = f.Base.SubmitSynthetic();
        var b = f.Base.SubmitSynthetic();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Automatic.RunCheckAsync(cts.Token));

        Assert.Equal(0, f.Base.RequirementsGenerator.CallCount);
        Assert.Equal(0, f.Base.DraftCount(a));
        Assert.Equal(0, f.Base.DraftCount(b));
    }

    [Fact]
    public async Task CancellationBetweenRequests_StopsBeforeTheNextOne()
    {
        var f = new AutomaticFixture();
        var a = f.Base.SubmitSynthetic();
        var b = f.Base.SubmitSynthetic();
        using var cts = new CancellationTokenSource();
        f.Base.RequirementsGenerator.Behavior = (_, _) =>
        {
            cts.Cancel();
            return Task.FromResult<AgenticChangeOps.Agents.Requirement.DraftContent?>(
                Fakes.FakeRequirementsGenerator.ValidContent());
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Automatic.RunCheckAsync(cts.Token));

        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);
        Assert.Equal(0, f.Base.DraftCount(b));
    }
}
