using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class ApprovalDecisionTests
{
    // ---- User Story 1: Approve a Submitted change request ----

    [Fact]
    public void Approve_SubmittedRequestWithoutReason_SetsStatusApproved()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(ChangeRequestStatus.Approved, result.ChangeRequest!.Status);
        Assert.Equal(ChangeRequestStatus.Approved, fx.Intake.GetById(id)!.Status);
    }

    [Fact]
    public void Approve_WithReason_StoresTrimmedReason()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Approve, reason: "  Low risk, synthetic scenario  "));

        var entry = fx.Intake.GetAuditHistory(id)![1];
        Assert.Equal("Low risk, synthetic scenario", entry.Reason);
    }

    [Fact]
    public void Approve_WithoutReason_StoresEmptyReason()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));

        var entry = fx.Intake.GetAuditHistory(id)![1];
        Assert.Equal(string.Empty, entry.Reason);
    }

    [Fact]
    public void Approve_RecordsSingleDecisionAuditEntryWithFullDetail()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.Clock.Now = fx.Clock.Now.AddMinutes(5);

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Approve, approver: "Jordan Reed"));

        var history = fx.Intake.GetAuditHistory(id)!;
        Assert.Equal(2, history.Count);
        var entry = history[1];
        Assert.Equal(AuditEventType.Approved, entry.EventType);
        Assert.Equal("Jordan Reed", entry.ApproverName);
        Assert.Equal(fx.Clock.Now, entry.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAt.Kind);
        Assert.Equal(ChangeRequestStatus.Submitted, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.Equal(id, entry.ChangeRequestId);
    }

    [Fact]
    public void Approve_AfterDefer_SucceedsBecauseDeferredIsDecidable()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Waiting for the maintenance window."));

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Approved, fx.Intake.GetById(id)!.Status);
    }

    // ---- User Story 2: Reject or Defer with a reason ----

    [Fact]
    public void Reject_SubmittedRequestWithReason_SetsStatusRejectedAndRecordsEntry()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Reject, reason: "Conflicts with the freeze window."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Rejected, result.ChangeRequest!.Status);
        var entry = fx.Intake.GetAuditHistory(id)![1];
        Assert.Equal(AuditEventType.Rejected, entry.EventType);
        Assert.Equal("Jordan Reed", entry.ApproverName);
        Assert.Equal("Conflicts with the freeze window.", entry.Reason);
        Assert.Equal(ChangeRequestStatus.Submitted, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Rejected, entry.StatusAfter);
    }

    [Fact]
    public void Defer_SubmittedRequestWithReason_SetsStatusDeferredAndRecordsEntry()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Needs capacity review first."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Deferred, result.ChangeRequest!.Status);
        var entry = fx.Intake.GetAuditHistory(id)![1];
        Assert.Equal(AuditEventType.Deferred, entry.EventType);
        Assert.Equal(ChangeRequestStatus.Submitted, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Deferred, entry.StatusAfter);
    }

    [Fact]
    public void Defer_AlreadyDeferredRequest_StaysDeferredAndAppendsSecondEntry()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "First deferral."));
        fx.Clock.Now = fx.Clock.Now.AddMinutes(1);

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Second deferral."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Deferred, fx.Intake.GetById(id)!.Status);
        var types = fx.Intake.GetAuditHistory(id)!.Select(e => e.EventType).ToArray();
        Assert.Equal(
            new[] { AuditEventType.Submitted, AuditEventType.Deferred, AuditEventType.Deferred },
            types);
    }

    [Fact]
    public void Reject_DeferredRequestWithReason_SetsStatusRejected()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Parked."));

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Reject, reason: "No longer needed."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Rejected, fx.Intake.GetById(id)!.Status);
    }
}
