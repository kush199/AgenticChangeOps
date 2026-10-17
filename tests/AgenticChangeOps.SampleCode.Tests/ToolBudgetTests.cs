using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>FR-012 to FR-014: per-request ceilings, enforced by our code, never by the model.</summary>
public sealed class ToolBudgetTests
{
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public void CallsUpToTheLimit_AreAdmitted_AndTheNextIsRefusedAndCounted()
    {
        var budget = new ToolBudget(maxCalls: 3, maxBytes: 1000, timeLimit: TimeSpan.FromSeconds(60));

        Assert.Null(budget.BeginCall());
        Assert.Null(budget.BeginCall());
        Assert.Null(budget.BeginCall());
        Assert.Equal(ToolCallOutcome.RefusedCallLimit, budget.BeginCall());
        Assert.Equal(ToolCallOutcome.RefusedCallLimit, budget.BeginCall());

        Assert.Equal(5, budget.CallsUsed);
    }

    [Fact]
    public void ARefusedCall_StillUsesTheBudget()
    {
        var budget = new ToolBudget(maxCalls: 2, maxBytes: 10, timeLimit: TimeSpan.FromSeconds(60));

        Assert.Null(budget.BeginCall());
        Assert.False(budget.TryTakeBytes(11));
        Assert.Null(budget.BeginCall());
        Assert.Equal(ToolCallOutcome.RefusedCallLimit, budget.BeginCall());
    }

    [Fact]
    public void AResultThatWouldPassTheDataLimit_IsRefusedWhole_AndASmallerOneCanStillBeTaken()
    {
        var budget = new ToolBudget(maxCalls: 10, maxBytes: 100, timeLimit: TimeSpan.FromSeconds(60));

        Assert.True(budget.TryTakeBytes(60));
        Assert.False(budget.TryTakeBytes(60));
        Assert.Equal(60, budget.BytesReturned);
        Assert.True(budget.TryTakeBytes(40));
        Assert.Equal(100, budget.BytesReturned);
        Assert.False(budget.TryTakeBytes(1));
    }

    [Fact]
    public void AfterTheTimeLimit_EveryCallIsRefused_AsTimeLimit()
    {
        var time = new ManualTime();
        var budget = new ToolBudget(maxCalls: 10, maxBytes: 100, timeLimit: TimeSpan.FromSeconds(30), time);

        Assert.Null(budget.BeginCall());
        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(ToolCallOutcome.RefusedTimeLimit, budget.BeginCall());
        Assert.False(budget.TryTakeBytes(1));
    }

    [Fact]
    public async Task ManyConcurrentCalls_NeverExceedEitherLimit()
    {
        var budget = new ToolBudget(maxCalls: 50, maxBytes: 500, timeLimit: TimeSpan.FromSeconds(60));
        var admitted = 0;
        var tookBytes = 0;

        await Task.WhenAll(Enumerable.Range(0, 400).Select(_ => Task.Run(() =>
        {
            if (budget.BeginCall() is null)
                Interlocked.Increment(ref admitted);
            if (budget.TryTakeBytes(10))
                Interlocked.Increment(ref tookBytes);
        })));

        Assert.Equal(50, admitted);
        Assert.Equal(50, tookBytes);
        Assert.Equal(500, budget.BytesReturned);
        Assert.Equal(400, budget.CallsUsed);
    }
}
