using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class NotConfiguredTests
{
    [Fact]
    public async Task WhenTheModelIsNotConfigured_NoStepRuns_AndNothingIsRecordedAsAFailure()
    {
        var f = new AutomaticFixture();
        f.Base.Availability.RequirementsConfigured = false;
        f.Base.Availability.DesignConfigured = false;
        var a = f.Base.SubmitSynthetic();
        var b = await f.Base.ApprovedWithDraftAsync();
        var callsBefore = f.Base.RequirementsGenerator.CallCount;

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(0, summary.Ran);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(callsBefore, f.Base.RequirementsGenerator.CallCount);
        Assert.Equal(0, f.Base.DesignGenerator.CallCount);
        Assert.Null(f.Store.GetAutomaticAttempt(a));
        Assert.Null(f.Store.GetAutomaticAttempt(b));
        Assert.Equal(0, f.Base.DraftCount(a));
        Assert.Equal(0, f.Base.DesignCount(b));
    }

    [Fact]
    public async Task TheStatusOfADueRequestStillNamesItsNextStep_WhenNotConfigured()
    {
        var f = new AutomaticFixture();
        f.Base.Availability.RequirementsConfigured = false;
        var id = f.Base.SubmitSynthetic();

        await f.Automatic.RunCheckAsync();

        Assert.Equal(WorkflowStep.RequirementsDraft, f.Status(id).NextStep);
    }

    [Fact]
    public async Task OnlyTheUnconfiguredStepIsSkipped()
    {
        var f = new AutomaticFixture();
        f.Base.Availability.DesignConfigured = false;
        var needsDraft = f.Base.SubmitSynthetic();
        var needsDesign = await f.Base.ApprovedWithDraftAsync();

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Ran);
        Assert.Equal(1, f.Base.DraftCount(needsDraft));
        Assert.Equal(0, f.Base.DesignCount(needsDesign));
    }
}
