using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class HandOverTests
{
    private static async Task<(AutomaticFixture F, Guid Id)> LeftForAPersonAsync()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 2, failureWaitSeconds: 60));
        var id = f.Base.SubmitSynthetic();
        f.RequirementsFail(GenerationFailureCategory.Unreachable);
        await f.Automatic.RunCheckAsync();
        f.Advance(TimeSpan.FromMinutes(2));
        await f.Automatic.RunCheckAsync();
        return (f, id);
    }

    [Fact]
    public async Task AfterTheLimit_TheRequestIsNeverAttemptedAgain_WhateverTimePasses()
    {
        var (f, id) = await LeftForAPersonAsync();
        var calls = f.Base.RequirementsGenerator.CallCount;

        for (var i = 0; i < 5; i++)
        {
            f.Advance(TimeSpan.FromDays(3));
            await f.Automatic.RunCheckAsync();
        }

        Assert.Equal(2, calls);
        Assert.Equal(calls, f.Base.RequirementsGenerator.CallCount);
        Assert.Equal(2, f.Store.GetAutomaticAttempt(id)!.ConsecutiveFailures);
    }

    [Fact]
    public async Task TheStatusShowsLeftForAPerson_WithTheCategory_AndKeepsItsNextStep()
    {
        var (f, id) = await LeftForAPersonAsync();

        var status = f.Status(id);

        Assert.Equal(
            new AutomaticAdvancementStatus(AutomaticMode.LeftForPerson, GenerationFailureCategory.Unreachable),
            status.Automatic);
        Assert.Equal(WorkflowStep.RequirementsDraft, status.NextStep);
        Assert.Null(status.Reason);
    }

    [Fact]
    public async Task AManualAdvance_StillWorks_AndTheRequestIsNoLongerLeftForAPerson()
    {
        var (f, id) = await LeftForAPersonAsync();
        f.RequirementsSucceed();

        var result = await f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.Advanced, result.Outcome);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(AutomaticMode.On, f.Status(id).Automatic!.Mode);
    }

    [Fact]
    public async Task ANewRequirementsDraft_MakesTheOldFailuresStopApplying_ToTheDesignStep()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 2, failureWaitSeconds: 60));
        var id = await f.Base.ApprovedWithDraftAsync();
        f.DesignFail();
        await f.Automatic.RunCheckAsync();
        f.Advance(TimeSpan.FromMinutes(2));
        await f.Automatic.RunCheckAsync();
        Assert.Equal(AutomaticMode.LeftForPerson, f.Status(id).Automatic!.Mode);

        f.Base.SeedStatus(id, ChangeRequestStatus.Deferred);
        await f.Base.Requirements.DraftAsync(id);
        f.Base.SeedStatus(id, ChangeRequestStatus.Approved);
        var designCallsBefore = f.Base.DesignGenerator.CallCount;

        Assert.Equal(AutomaticMode.On, f.Status(id).Automatic!.Mode);
        await f.Automatic.RunCheckAsync();

        Assert.Equal(designCallsBefore + 1, f.Base.DesignGenerator.CallCount);
    }

    [Fact]
    public async Task OneRequestLeftForAPerson_DoesNotBlockOthers()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 1));
        var bad = f.Base.SubmitSynthetic();
        f.Base.RequirementsGenerator.Behavior = (request, _) =>
            request.ChangeRequestId == bad
                ? throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput)
                : Task.FromResult<DraftContent?>(FakeRequirementsGenerator.ValidContent());
        await f.Automatic.RunCheckAsync();
        Assert.Equal(AutomaticMode.LeftForPerson, f.Status(bad).Automatic!.Mode);

        var good = f.Base.SubmitSynthetic();
        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Ran);
        Assert.Equal(1, f.Base.DraftCount(good));
    }
}
