using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class FailureWaitTests
{
    [Fact]
    public async Task AFailedStep_StoresNothing_AndWritesOneAttemptRow()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.RequirementsFail(GenerationFailureCategory.Unreachable);
        var auditBefore = f.Base.AuditTypes(id);

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Failed);
        Assert.Equal(0, summary.Ran);
        Assert.Equal(0, f.Base.DraftCount(id));
        Assert.Equal(auditBefore, f.Base.AuditTypes(id));
        var row = f.Store.GetAutomaticAttempt(id)!;
        Assert.Equal("RequirementsDraft", row.Step);
        Assert.Equal(0, row.CompletedStepCount);
        Assert.Equal(1, row.ConsecutiveFailures);
        Assert.Equal("Unreachable", row.LastFailureCategory);
        Assert.Equal(f.Base.Clock.UtcNow, row.LastFailureAt);
    }

    [Fact]
    public async Task ThereIsNoRetryBeforeTheWaitHasPassed_AndOneAtExactlyTheWait()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(failureWaitSeconds: 900));
        f.Base.SubmitSynthetic();
        f.RequirementsFail();
        await f.Automatic.RunCheckAsync();
        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);

        f.Advance(TimeSpan.FromSeconds(899));
        var early = await f.Automatic.RunCheckAsync();

        Assert.Equal(0, early.Failed);
        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);

        f.Advance(TimeSpan.FromSeconds(1));
        var onTime = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, onTime.Failed);
        Assert.Equal(2, f.Base.RequirementsGenerator.CallCount);
    }

    [Fact]
    public async Task ASecondFailure_RaisesTheCount()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.RequirementsFail();

        await f.Automatic.RunCheckAsync();
        f.Advance(TimeSpan.FromMinutes(20));
        await f.Automatic.RunCheckAsync();

        Assert.Equal(2, f.Store.GetAutomaticAttempt(id)!.ConsecutiveFailures);
    }

    [Fact]
    public async Task AFailureWithNoCategory_BecauseTheFactsChangedMidStep_IsNotCounted()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.Base.RequirementsGenerator.Behavior = (_, _) =>
        {
            f.Base.Decide(id, ApprovalDecision.Reject);
            return Task.FromResult<DraftContent?>(FakeRequirementsGenerator.ValidContent());
        };

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Ran);
        Assert.Null(f.Store.GetAutomaticAttempt(id));
        Assert.Equal(0, f.Base.DraftCount(id));
    }

    [Fact]
    public async Task ASuccess_DeletesTheRow()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.RequirementsFail();
        await f.Automatic.RunCheckAsync();
        Assert.NotNull(f.Store.GetAutomaticAttempt(id));

        f.RequirementsSucceed();
        f.Advance(TimeSpan.FromMinutes(20));
        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Ran);
        Assert.Null(f.Store.GetAutomaticAttempt(id));
    }

    [Fact]
    public async Task OneRequestsFailure_DoesNotStopOthersInTheSameCheck()
    {
        var f = new AutomaticFixture();
        var bad = f.Base.SubmitSynthetic();
        var good = f.Base.SubmitSynthetic();
        f.Base.RequirementsGenerator.Behavior = (request, _) =>
            request.ChangeRequestId == bad
                ? throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput)
                : Task.FromResult<DraftContent?>(FakeRequirementsGenerator.ValidContent());

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Ran);
        Assert.Equal(1, f.Base.DraftCount(good));
        Assert.Equal(0, f.Base.DraftCount(bad));
    }

    [Fact]
    public async Task TheSummaryCountsFailuresPerCategory_AndTheTotalMatches()
    {
        var f = new AutomaticFixture();
        f.Base.SubmitSynthetic();
        f.RequirementsFail(GenerationFailureCategory.TimedOut);

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.FailuresByCategory[GenerationFailureCategory.TimedOut]);
        Assert.Equal(summary.Failed, summary.FailuresByCategory.Values.Sum());
    }
}
