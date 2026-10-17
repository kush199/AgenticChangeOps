using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class DueSelectionTests
{
    [Fact]
    public async Task ANewRequest_GetsExactlyOneRequirementsDraft_AndItsStatusIsUnchanged()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        Assert.Equal(WorkflowStep.RequirementsDraft, f.Status(id).NextStep);

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Ran);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(new[] { AuditEventType.Submitted, AuditEventType.DraftGenerated }, f.Base.AuditTypes(id));
        Assert.Equal(ChangeRequestStatus.Submitted, f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task AnApprovedRequestWithRequirements_GetsExactlyOneDesignDraft()
    {
        var f = new AutomaticFixture();
        var id = await f.Base.ApprovedWithDraftAsync();
        Assert.Equal(WorkflowStep.DesignDraft, f.Status(id).NextStep);

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, summary.Ran);
        Assert.Equal(1, f.Base.DesignCount(id));
        Assert.Equal(AuditEventType.DesignGenerated, f.Base.AuditTypes(id)[^1]);
        Assert.Equal(ChangeRequestStatus.Approved, f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task AnApprovedRequestWithNoRequirementsDraft_GetsItsDraftAutomatically_ThenItsDesign()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.Base.Decide(id, ApprovalDecision.Approve);
        Assert.Equal(WorkflowStep.RequirementsDraft, f.Status(id).NextStep);

        var first = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, first.Ran);
        Assert.Equal(0, first.Failed);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(
            new[] { AuditEventType.Submitted, AuditEventType.Approved, AuditEventType.DraftGenerated },
            f.Base.AuditTypes(id));
        Assert.True(f.Store.GetAuditEntries(id).Last().Automatic);
        Assert.Equal(ChangeRequestStatus.Approved, f.Store.FindById(id)!.Status);
        Assert.Equal(WorkflowStep.DesignDraft, f.Status(id).NextStep);

        var second = await f.Automatic.RunCheckAsync();

        Assert.Equal(1, second.Ran);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(1, f.Base.DesignCount(id));
    }

    [Fact]
    public async Task TheSystemItselfNeverRecordsTheApproval_TheDesignWaitsForAPerson()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();

        await f.Automatic.RunCheckAsync();
        await f.Automatic.RunCheckAsync();

        Assert.Equal(0, f.Base.DesignCount(id));
        Assert.Equal(ChangeRequestStatus.Submitted, f.Store.FindById(id)!.Status);

        f.Base.Decide(id, ApprovalDecision.Approve);
        await f.Automatic.RunCheckAsync();

        Assert.Equal(1, f.Base.DesignCount(id));
    }

    [Theory]
    [InlineData("awaiting")]
    [InlineData("rejected")]
    [InlineData("deferred")]
    public async Task ARequestAwaitingADecision_RejectedOrDeferredWithADraft_ReceivesNothing(string situation)
    {
        var f = new AutomaticFixture();
        var id = await f.Base.SubmittedWithDraftAsync();
        if (situation == "rejected") f.Base.Decide(id, ApprovalDecision.Reject);
        if (situation == "deferred") f.Base.Decide(id, ApprovalDecision.Defer);
        var auditBefore = f.Base.AuditTypes(id);
        var callsBefore = f.Base.RequirementsGenerator.CallCount;

        var summary = await f.Automatic.RunCheckAsync();

        Assert.Equal(0, summary.Ran);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(0, f.Base.DesignCount(id));
        Assert.Equal(auditBefore, f.Base.AuditTypes(id));
        Assert.Equal(callsBefore, f.Base.RequirementsGenerator.CallCount);
        Assert.Equal(0, f.Base.DesignGenerator.CallCount);
    }

    [Fact]
    public async Task ADeferredRequestWithNoDraft_GetsItsRequirementsDraft_AsTheManualAdvanceWould()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();
        f.Base.SeedStatus(id, ChangeRequestStatus.Deferred);
        Assert.Equal(WorkflowStep.RequirementsDraft, f.Status(id).NextStep);

        await f.Automatic.RunCheckAsync();

        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(ChangeRequestStatus.Deferred, f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task AStepAlreadyCompleteForTheCurrentInputs_IsNeverRunAgain()
    {
        var f = new AutomaticFixture();
        var id = f.Base.SubmitSynthetic();

        for (var i = 0; i < 4; i++)
        {
            await f.Automatic.RunCheckAsync();
            f.Advance(TimeSpan.FromMinutes(10));
        }

        Assert.Equal(1, f.Base.DraftCount(id));
        Assert.Equal(1, f.Base.RequirementsGenerator.CallCount);
    }

    [Fact]
    public async Task ANewRequirementsDraftAfterADesign_MakesTheDesignStale_AndANewDesignIsRun()
    {
        var f = new AutomaticFixture();
        var id = await f.Base.ApprovedWithDraftAsync();
        await f.Automatic.RunCheckAsync();
        Assert.Equal(1, f.Base.DesignCount(id));

        f.Base.SeedStatus(id, ChangeRequestStatus.Deferred);
        await f.Base.Requirements.DraftAsync(id);
        f.Base.SeedStatus(id, ChangeRequestStatus.Approved);
        await f.Automatic.RunCheckAsync();

        Assert.Equal(2, f.Base.DesignCount(id));
        Assert.Equal(2, f.Base.Store.GetDesigns(id)[^1].RequirementsVersion);
    }

    [Fact]
    public async Task NoAutomaticStep_EverChangesAStatusOrRecordsADecision()
    {
        var f = new AutomaticFixture();
        var ids = new[] { f.Base.SubmitSynthetic(), await f.Base.ApprovedWithDraftAsync(), f.Base.SubmitSynthetic() };
        var before = ids.Select(i => f.Store.FindById(i)!.Status).ToArray();
        var decisionsBefore = ids.Select(i => DecisionEntries(f, i)).ToArray();

        for (var i = 0; i < 3; i++)
            await f.Automatic.RunCheckAsync();

        Assert.Equal(before, ids.Select(i => f.Store.FindById(i)!.Status).ToArray());
        Assert.Equal(decisionsBefore, ids.Select(i => DecisionEntries(f, i)).ToArray());
    }

    private static int DecisionEntries(AutomaticFixture f, Guid id) =>
        f.Base.AuditTypes(id).Count(t => t is AuditEventType.Approved or AuditEventType.Rejected or AuditEventType.Deferred);
}
