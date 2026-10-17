using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class CheckSummaryTests
{
    [Fact]
    public async Task EachCheck_ReturnsTheClockTime_AndTheExpectedCounts()
    {
        var f = new AutomaticFixture();
        f.Base.SubmitSynthetic();
        f.Base.SubmitSynthetic();
        await f.Base.SubmittedWithDraftAsync();
        var now = f.Base.Clock.UtcNow;

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(now, summary.CheckedAt);
        Assert.Equal(3, summary.Examined);
        Assert.Equal(2, summary.Ran);
        Assert.Equal(0, summary.Failed);
        Assert.Empty(summary.FailuresByCategory);
    }

    [Fact]
    public async Task AMixedCheck_CountsRunAndFailedSeparately_WithCategories()
    {
        var f = new AutomaticFixture();
        var bad = f.Base.SubmitSynthetic();
        f.Base.SubmitSynthetic();
        f.Base.RequirementsGenerator.Behavior = (request, _) =>
            request.ChangeRequestId == bad
                ? throw new DraftGenerationException(GenerationFailureCategory.ResponseTooLarge)
                : Task.FromResult<AgenticChangeOps.Agents.Requirement.DraftContent?>(
                    Fakes.FakeRequirementsGenerator.ValidContent());

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Ran);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.FailuresByCategory[GenerationFailureCategory.ResponseTooLarge]);
    }

    [Fact]
    public async Task AnUnexpectedError_CountsInFailed_ButInNoCategory()
    {
        var f = new AutomaticFixture(availability: new ThrowOnFirstCallAvailability());
        f.Base.SubmitSynthetic();

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Failed);
        Assert.Empty(summary.FailuresByCategory);
    }

    [Fact]
    public void TheHolder_IsEmptyUntilTheFirstRecord_ThenKeepsTheLatest()
    {
        var holder = new CheckSummaryHolder();
        Assert.Null(holder.Latest);

        var first = new CheckSummary(DateTime.UtcNow, 1, 1, 0, new Dictionary<GenerationFailureCategory, int>());
        var second = first with { Examined = 9 };
        holder.Record(first);
        holder.Record(second);

        Assert.Same(second, holder.Latest);
    }

    [Fact]
    public async Task TheHolder_IsSafeUnderConcurrentRecordAndRead()
    {
        var holder = new CheckSummaryHolder();
        var empty = new Dictionary<GenerationFailureCategory, int>();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            holder.Record(new CheckSummary(DateTime.UtcNow, i, 0, 0, empty));
            Assert.NotNull(holder.Latest);
        })));

        Assert.NotNull(holder.Latest);
    }
}
