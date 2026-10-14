using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

public class AdvanceConcurrencyTests
{
    [Fact]
    public async Task TenSimultaneousAdvances_StoreOneDraft_AndNineReportNothingDue()
    {
        var f = new WorkflowTestFixture();
        var id = f.SubmitSynthetic();
        f.RequirementsGenerator.Behavior = async (_, ct) =>
        {
            await Task.Delay(30, ct);
            return FakeRequirementsGenerator.ValidContent();
        };

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => f.Workflow.AdvanceAsync(id))));

        Assert.Equal(1, results.Count(r => r.Outcome == AdvanceOutcome.Advanced));
        Assert.Equal(9, results.Count(r => r.Outcome == AdvanceOutcome.NothingDue));
        Assert.Equal(1, f.DraftCount(id));
        Assert.Equal(1, f.RequirementsGenerator.CallCount);
    }

    [Fact]
    public async Task AdvancesForDifferentRequests_DoNotBlockEachOther()
    {
        var f = new WorkflowTestFixture();
        var a = f.SubmitSynthetic();
        var b = f.SubmitSynthetic();
        var inFlight = 0;
        var peak = 0;
        var bothArrived = new TaskCompletionSource();
        f.RequirementsGenerator.Behavior = async (_, ct) =>
        {
            var now = Interlocked.Increment(ref inFlight);
            lock (bothArrived) peak = Math.Max(peak, now);
            if (now == 2)
                bothArrived.TrySetResult();
            await Task.WhenAny(bothArrived.Task, Task.Delay(TimeSpan.FromSeconds(5), ct));
            Interlocked.Decrement(ref inFlight);
            return FakeRequirementsGenerator.ValidContent();
        };

        await Task.WhenAll(
            Task.Run(() => f.Workflow.AdvanceAsync(a)),
            Task.Run(() => f.Workflow.AdvanceAsync(b)));

        Assert.Equal(2, peak);
        Assert.Equal(1, f.DraftCount(a));
        Assert.Equal(1, f.DraftCount(b));
    }
}
