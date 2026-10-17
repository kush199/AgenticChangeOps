using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class BudgetTests
{
    [Fact]
    public async Task NoCheckRunsMoreThanTheMaximum_AndLaterChecksPickUpTheRest()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxSteps: 3));
        var ids = Enumerable.Range(0, 7).Select(_ => f.Base.SubmitSynthetic()).ToArray();

        var first = await f.Automatic.RunCheckAsync();
        var second = await f.Automatic.RunCheckAsync();
        var third = await f.Automatic.RunCheckAsync();
        var fourth = await f.Automatic.RunCheckAsync();

        Assert.Equal(3, first.Ran);
        Assert.Equal(3, second.Ran);
        Assert.Equal(1, third.Ran);
        Assert.Equal(0, fourth.Ran);
        Assert.All(ids, id => Assert.Equal(1, f.Base.DraftCount(id)));
    }

    [Fact]
    public async Task RequestsAreHandledInTheOrderTheyWereStored()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxSteps: 2));
        var ids = Enumerable.Range(0, 4).Select(_ => f.Base.SubmitSynthetic()).ToArray();

        await f.Automatic.RunCheckAsync();

        Assert.Equal(1, f.Base.DraftCount(ids[0]));
        Assert.Equal(1, f.Base.DraftCount(ids[1]));
        Assert.Equal(0, f.Base.DraftCount(ids[2]));
        Assert.Equal(0, f.Base.DraftCount(ids[3]));
    }

    [Fact]
    public async Task ExaminedCountsTheRequestsLookedAt()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxSteps: 3));
        for (var i = 0; i < 7; i++) f.Base.SubmitSynthetic();

        var first = await f.Automatic.RunCheckAsync();
        await f.Automatic.RunCheckAsync();
        var third = await f.Automatic.RunCheckAsync();

        Assert.Equal(3, first.Examined);
        Assert.Equal(7, third.Examined);
    }

    [Fact]
    public async Task FailedAttemptsUseBudgetToo()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxSteps: 2));
        for (var i = 0; i < 4; i++) f.Base.SubmitSynthetic();
        f.RequirementsFail();

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(2, summary.Failed);
        Assert.Equal(2, f.Base.RequirementsGenerator.CallCount);
    }

    [Fact]
    public async Task ASkippedRequest_DoesNotUseBudget()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxSteps: 1));
        var a = f.Base.SubmitSynthetic();
        var b = f.Base.SubmitSynthetic();
        f.Base.RequirementsGenerator.Behavior = (request, _) =>
            request.ChangeRequestId == a
                ? throw new DraftGenerationException(GenerationFailureCategory.TimedOut)
                : Task.FromResult<DraftContent?>(FakeRequirementsGenerator.ValidContent());

        var first = await f.Automatic.RunCheckAsync();
        var second = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, first.Failed);
        Assert.Equal(0, first.Ran);
        Assert.Equal(1, second.Ran);
        Assert.Equal(0, second.Failed);
        Assert.Equal(1, f.Base.DraftCount(b));
        Assert.Equal(0, f.Base.DraftCount(a));
    }
}
