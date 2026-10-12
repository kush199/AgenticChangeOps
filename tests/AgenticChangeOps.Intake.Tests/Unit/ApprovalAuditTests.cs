using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class ApprovalAuditTests
{
    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_DeferDeferApprove_ListsSubmissionAndDecisionsInOrder()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        fx.Clock.Now = fx.Clock.Now.AddMinutes(1);
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Awaiting vendor window."));
        fx.Clock.Now = fx.Clock.Now.AddMinutes(1);
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Still awaiting vendor window."));
        fx.Clock.Now = fx.Clock.Now.AddMinutes(1);
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));

        var history = fx.Intake.GetAuditHistory(id)!;

        Assert.Equal(
            new[]
            {
                AuditEventType.Submitted, AuditEventType.Deferred,
                AuditEventType.Deferred, AuditEventType.Approved
            },
            history.Select(e => e.EventType).ToArray());
        Assert.Equal(4, history.Select(e => e.AuditEntryId).Distinct().Count());
        Assert.Equal(
            new[] { "Awaiting vendor window.", "Still awaiting vendor window.", string.Empty },
            history.Skip(1).Select(e => e.Reason).ToArray());
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_DecisionsWithIdenticalTimestamps_KeepProcessingOrder()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        // Clock is not advanced: submission and both decisions share one timestamp.
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "First."));
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Reject, reason: "Second."));

        var history = fx.Intake.GetAuditHistory(id)!;

        Assert.Equal(
            new[] { AuditEventType.Submitted, AuditEventType.Deferred, AuditEventType.Rejected },
            history.Select(e => e.EventType).ToArray());
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_FailedDecisionBetweenSuccesses_AddsNoEntry()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Parked."));
        var failed = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Reject, reason: null));
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));

        Assert.False(failed.IsSuccess);
        var history = fx.Intake.GetAuditHistory(id)!;
        Assert.Equal(3, history.Count); // 1 submission + 2 successful decisions
        Assert.DoesNotContain(history, e => e.EventType == AuditEventType.Rejected);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_DecisionsOnOneRequest_DoNotAppearOnAnother()
    {
        var fx = new ApprovalTestFixture();
        var idA = fx.SubmitSynthetic();
        var idB = fx.SubmitSynthetic();

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(idA, ApprovalDecision.Approve));

        Assert.Equal(2, fx.Intake.GetAuditHistory(idA)!.Count);
        var historyB = fx.Intake.GetAuditHistory(idB)!;
        Assert.Single(historyB);
        Assert.Equal(AuditEventType.Submitted, historyB[0].EventType);
    }
}
