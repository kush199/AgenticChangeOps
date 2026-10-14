using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

public class AdvanceRefusalTests
{
    private readonly WorkflowTestFixture _f = new();

    private async Task AssertRefusedUnchanged(Guid id, WorkflowReasonCode code)
    {
        var auditBefore = _f.AuditTypes(id);
        var statusBefore = _f.Workflow.GetStatus(id).Value;
        var requirementsCalls = _f.RequirementsGenerator.CallCount;
        var designCalls = _f.DesignGenerator.CallCount;

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.NothingDue, result.Outcome);
        Assert.Equal(code, result.Status!.Reason!.Code);
        Assert.Equal(statusBefore, result.Status);
        Assert.Equal(auditBefore, _f.AuditTypes(id));
        Assert.Equal(requirementsCalls, _f.RequirementsGenerator.CallCount);
        Assert.Equal(designCalls, _f.DesignGenerator.CallCount);
    }

    [Fact]
    public async Task SubmittedWithDraft_AwaitsDecision()
    {
        var id = await _f.SubmittedWithDraftAsync();
        await AssertRefusedUnchanged(id, WorkflowReasonCode.AwaitingDecision);
        Assert.Equal(1, _f.DraftCount(id));
    }

    [Fact]
    public async Task UnderReview_AwaitsDecision()
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, ChangeRequestStatus.UnderReview);
        await AssertRefusedUnchanged(id, WorkflowReasonCode.AwaitingDecision);
    }

    [Fact]
    public async Task Rejected_IsRefused()
    {
        var id = await _f.SubmittedWithDraftAsync();
        _f.Decide(id, ApprovalDecision.Reject);
        await AssertRefusedUnchanged(id, WorkflowReasonCode.Rejected);
    }

    [Fact]
    public async Task DeferredWithDraft_IsRefused()
    {
        var id = await _f.SubmittedWithDraftAsync();
        _f.Decide(id, ApprovalDecision.Defer);
        await AssertRefusedUnchanged(id, WorkflowReasonCode.Deferred);
    }

    [Fact]
    public async Task ApprovedWithoutRequirementsDraft_NeedsOne()
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, ChangeRequestStatus.Approved);
        await AssertRefusedUnchanged(id, WorkflowReasonCode.RequirementsDraftRequired);
    }

    [Fact]
    public async Task ApprovedWithCurrentDesign_NothingDue_AndNoNewVersion()
    {
        var id = await _f.ApprovedWithDraftAsync();
        await _f.Workflow.AdvanceAsync(id);

        await AssertRefusedUnchanged(id, WorkflowReasonCode.NothingDue);

        Assert.Equal(1, _f.DesignCount(id));
    }

    [Fact]
    public async Task DueStepWithModelNotConfigured_IsNotConfigured_AndStoresNothing()
    {
        var id = await _f.ApprovedWithDraftAsync();
        _f.Availability.DesignConfigured = false;

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.NotConfigured, result.Outcome);
        Assert.Equal(WorkflowStep.DesignDraft, result.Step);
        Assert.Equal(0, _f.DesignCount(id));
        Assert.Equal(0, _f.DesignGenerator.CallCount);
        Assert.DoesNotContain(AuditEventType.DesignGenerated, _f.AuditTypes(id));
    }

    [Fact]
    public async Task NothingDue_WinsOverNotConfigured()
    {
        var id = await _f.SubmittedWithDraftAsync();
        _f.Availability.RequirementsConfigured = false;
        _f.Availability.DesignConfigured = false;

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.NothingDue, result.Outcome);
    }
}
