using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

public class AdvanceTests
{
    private readonly WorkflowTestFixture _f = new();

    [Fact]
    public async Task Advance_OnFreshRequest_StoresOneRequirementsDraftWithItsAuditEntry()
    {
        var id = _f.SubmitSynthetic();

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.Advanced, result.Outcome);
        Assert.Equal(WorkflowStep.RequirementsDraft, result.Step);
        Assert.True(result.Requirements!.IsSuccess);
        Assert.Equal(1, result.Requirements.Draft!.Version);
        Assert.Equal(1, _f.DraftCount(id));
        Assert.Equal(
            new[] { AuditEventType.Submitted, AuditEventType.DraftGenerated }, _f.AuditTypes(id));
        Assert.Equal(WorkflowReasonCode.AwaitingDecision, result.Status!.Reason!.Code);
    }

    [Fact]
    public async Task Advance_OnApprovedRequest_StoresOneDesignWithItsAuditEntry()
    {
        var id = await _f.ApprovedWithDraftAsync();

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.Advanced, result.Outcome);
        Assert.Equal(WorkflowStep.DesignDraft, result.Step);
        Assert.Equal(1, result.Design!.Draft!.Version);
        Assert.Equal(1, result.Design.Draft.RequirementsVersion);
        Assert.Equal(1, _f.DesignCount(id));
        Assert.Equal(
            new[]
            {
                AuditEventType.Submitted, AuditEventType.DraftGenerated,
                AuditEventType.Approved, AuditEventType.DesignGenerated
            },
            _f.AuditTypes(id));
        Assert.Equal(WorkflowStage.DesignComplete, result.Status!.Stage);
    }

    [Fact]
    public async Task Advance_Audit_IsIdenticalToCallingTheAgentDirectly()
    {
        var viaWorkflow = new WorkflowTestFixture();
        var direct = new WorkflowTestFixture();
        var a = viaWorkflow.SubmitSynthetic();
        var b = direct.SubmitSynthetic();

        var advanced = await viaWorkflow.Workflow.AdvanceAsync(a);
        var called = await direct.Requirements.DraftAsync(b);

        Assert.Equal(direct.Store.GetAuditEntries(b).Select(e => (e.EventType, e.AgentName, e.DraftVersion, e.StatusBefore, e.StatusAfter)),
            viaWorkflow.Store.GetAuditEntries(a).Select(e => (e.EventType, e.AgentName, e.DraftVersion, e.StatusBefore, e.StatusAfter)));
        Assert.Equal(called.Draft!.AgentName, advanced.Requirements!.Draft!.AgentName);
        Assert.Equal(called.Draft.Summary, advanced.Requirements.Draft.Summary);
    }

    [Fact]
    public async Task Advance_NeverChangesStatusOrRecordsADecision()
    {
        var id = await _f.ApprovedWithDraftAsync();
        var before = _f.Store.FindById(id);
        var decisions = _f.AuditTypes(id).Count(t => t is AuditEventType.Approved or AuditEventType.Rejected or AuditEventType.Deferred);

        await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(before, _f.Store.FindById(id));
        Assert.Equal(
            decisions,
            _f.AuditTypes(id).Count(t => t is AuditEventType.Approved or AuditEventType.Rejected or AuditEventType.Deferred));
    }

    [Fact]
    public async Task OneAdvance_RunsExactlyOneStep()
    {
        var id = _f.SubmitSynthetic();

        await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(1, _f.RequirementsGenerator.CallCount);
        Assert.Equal(0, _f.DesignGenerator.CallCount);
    }

    [Fact]
    public async Task Advance_OnDeferredRequestWithNoDraft_RunsRequirementsDraftAndNoFurther()
    {
        var id = _f.SubmitSynthetic();
        _f.Decide(id, ApprovalDecision.Defer);

        var first = await _f.Workflow.AdvanceAsync(id);
        var second = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.Advanced, first.Outcome);
        Assert.Equal(WorkflowStep.RequirementsDraft, first.Step);
        Assert.Equal(AdvanceOutcome.NothingDue, second.Outcome);
        Assert.Equal(WorkflowReasonCode.Deferred, second.Status!.Reason!.Code);
        Assert.Equal(0, _f.DesignGenerator.CallCount);
    }

    [Fact]
    public async Task Advance_StatusAfter_EqualsStatusRead()
    {
        var id = _f.SubmitSynthetic();

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(_f.Workflow.GetStatus(id).Value, result.Status);
    }
}
