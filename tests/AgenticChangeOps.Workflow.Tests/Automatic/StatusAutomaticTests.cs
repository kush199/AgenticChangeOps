using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class StatusAutomaticTests
{
    private static AutomaticAttempt Row(Guid id, int failures, int completed = 0, string step = "RequirementsDraft") =>
        new(id, step, completed, failures, "Unreachable", new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void WithTheFeatureOff_TheModeIsOff_WhateverRowsExist()
    {
        var f = new AutomaticFixture(AutomaticFixture.Off());
        var id = f.Base.SubmitSynthetic();
        f.Store.SaveAutomaticAttempt(Row(id, failures: 3));

        Assert.Equal(new AutomaticAdvancementStatus(AutomaticMode.Off), f.Status(id).Automatic);
    }

    [Fact]
    public void WithTheFeatureOnAndNoRow_TheModeIsOn()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();

        Assert.Equal(new AutomaticAdvancementStatus(AutomaticMode.On), f.Status(id).Automatic);
    }

    [Fact]
    public void ARowBelowTheLimit_IsStillOn()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 3));
        var id = f.Base.SubmitSynthetic();
        f.Store.SaveAutomaticAttempt(Row(id, failures: 2));

        Assert.Equal(AutomaticMode.On, f.Status(id).Automatic!.Mode);
        Assert.Null(f.Status(id).Automatic!.FailureCategory);
    }

    [Fact]
    public void ARowAtTheLimit_IsLeftForAPerson_WithItsCategoryAndNothingElse()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 3));
        var id = f.Base.SubmitSynthetic();
        f.Store.SaveAutomaticAttempt(Row(id, failures: 3));

        Assert.Equal(
            new AutomaticAdvancementStatus(AutomaticMode.LeftForPerson, GenerationFailureCategory.Unreachable),
            f.Status(id).Automatic);
    }

    [Theory]
    [InlineData(1, "RequirementsDraft")]
    [InlineData(0, "DesignDraft")]
    public void ARowForADifferentSituation_DoesNotApply(int completed, string step)
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 3));
        var id = f.Base.SubmitSynthetic();
        f.Store.SaveAutomaticAttempt(Row(id, failures: 3, completed, step));

        Assert.Equal(AutomaticMode.On, f.Status(id).Automatic!.Mode);
    }

    [Fact]
    public async Task EverythingElseInTheStatus_IsIdenticalToAServiceWithoutTheSource()
    {
        var f = new AutomaticFixture();
        var plain = new WorkflowService(
            f.Store, f.Base.Requirements, f.Base.Designs, f.Base.Availability);
        var id = await f.Base.ApprovedWithDraftAsync();

        var with = f.Status(id);
        var without = plain.GetStatus(id).Value!;

        Assert.Null(without.Automatic);
        Assert.Equal(without.ChangeRequestId, with.ChangeRequestId);
        Assert.Equal(without.ChangeRequestStatus, with.ChangeRequestStatus);
        Assert.Equal(without.Stage, with.Stage);
        Assert.Equal(without.NextStep, with.NextStep);
        Assert.Equal(without.Reason, with.Reason);
        Assert.True(without.CompletedSteps.SequenceEqual(with.CompletedSteps));
    }

    [Fact]
    public async Task TheAdvanceResultCarriesTheSameAutomaticBlock()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();

        var result = await f.Workflow.AdvanceAsync(id);

        Assert.Equal(AutomaticMode.On, result.Status!.Automatic!.Mode);
    }
}
