using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

public class WorkflowStatusTests
{
    private readonly WorkflowTestFixture _f = new();

    private WorkflowStatus Status(Guid id)
    {
        var result = _f.Workflow.GetStatus(id);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public void FreshRequest_NamesRequirementsDraftAsNext()
    {
        var id = _f.SubmitSynthetic();

        var status = Status(id);

        Assert.Equal(WorkflowStage.AwaitingRequirementsDraft, status.Stage);
        Assert.Equal(WorkflowStep.RequirementsDraft, status.NextStep);
        Assert.Empty(status.CompletedSteps);
    }

    [Fact]
    public async Task AfterRequirementsDraft_ListsItAndAwaitsDecision()
    {
        var id = await _f.SubmittedWithDraftAsync();

        var status = Status(id);

        var step = Assert.Single(status.CompletedSteps);
        Assert.Equal(WorkflowStep.RequirementsDraft, step.Step);
        Assert.Equal(1, step.Version);
        Assert.Equal(WorkflowReasonCode.AwaitingDecision, status.Reason!.Code);
        Assert.Null(status.NextStep);
    }

    [Fact]
    public async Task AfterApproval_NamesDesignAsNext_WithNoOtherUpdate()
    {
        var id = await _f.SubmittedWithDraftAsync();
        Assert.Null(Status(id).NextStep);

        _f.Decide(id, ApprovalDecision.Approve);

        Assert.Equal(WorkflowStep.DesignDraft, Status(id).NextStep);
        Assert.Equal(WorkflowStage.ReadyForDesign, Status(id).Stage);
    }

    [Fact]
    public async Task AfterDesign_ListsBothStepsAndIsComplete()
    {
        var id = await _f.ApprovedWithDraftAsync();
        (await _f.Designs.DesignAsync(id)).Draft!.ToString();

        var status = Status(id);

        Assert.Equal(WorkflowStage.DesignComplete, status.Stage);
        Assert.Equal(WorkflowReasonCode.NothingDue, status.Reason!.Code);
        Assert.Equal(2, status.CompletedSteps.Count);
        Assert.Equal(1, status.CompletedSteps[1].RequirementsVersion);
    }

    [Theory]
    [InlineData(ApprovalDecision.Reject, WorkflowStage.Rejected, WorkflowReasonCode.Rejected)]
    [InlineData(ApprovalDecision.Defer, WorkflowStage.Deferred, WorkflowReasonCode.Deferred)]
    public async Task DecidedAgainst_NamesTheStatusAndOffersNothing(
        ApprovalDecision decision, WorkflowStage stage, WorkflowReasonCode code)
    {
        var id = await _f.SubmittedWithDraftAsync();
        _f.Decide(id, decision);

        var status = Status(id);

        Assert.Equal(stage, status.Stage);
        Assert.Equal(code, status.Reason!.Code);
        Assert.Null(status.NextStep);
    }

    [Fact]
    public void UnknownId_IsAChangeRequestIdError()
    {
        var result = _f.Workflow.GetStatus(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("ChangeRequestId", Assert.Single(result.Errors).FieldName);
    }

    [Fact]
    public async Task Status_WritesNothing_AndNeverCallsAGenerator()
    {
        var id = await _f.ApprovedWithDraftAsync();
        var auditBefore = _f.AuditTypes(id);
        var requirementsCalls = _f.RequirementsGenerator.CallCount;

        for (var i = 0; i < 3; i++)
            Status(id);

        Assert.Equal(auditBefore, _f.AuditTypes(id));
        Assert.Equal(1, _f.DraftCount(id));
        Assert.Equal(0, _f.DesignCount(id));
        Assert.Equal(requirementsCalls, _f.RequirementsGenerator.CallCount);
        Assert.Equal(0, _f.DesignGenerator.CallCount);
    }
}
