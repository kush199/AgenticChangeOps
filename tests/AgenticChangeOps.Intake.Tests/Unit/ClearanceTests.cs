using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class ClearanceTests
{
    [Theory]
    [InlineData(ChangeRequestStatus.Submitted, false)]
    [InlineData(ChangeRequestStatus.UnderReview, false)]
    [InlineData(ChangeRequestStatus.Approved, true)]
    [InlineData(ChangeRequestStatus.Rejected, false)]
    [InlineData(ChangeRequestStatus.Deferred, false)]
    public void IsClearedToProceed_ReturnsTrueOnlyForApproved(
        ChangeRequestStatus status, bool expected)
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.SeedStatus(id, status);

        Assert.Equal(expected, fx.Approval.IsClearedToProceed(id));
    }

    [Fact]
    public void IsClearedToProceed_AllStatusValuesAreCovered()
    {
        // Guards the theory above: adding a status value must force a decision here.
        Assert.Equal(5, Enum.GetValues<ChangeRequestStatus>().Length);
    }

    [Fact]
    public void IsClearedToProceed_UnknownId_ReturnsFalseWithoutThrowing()
    {
        var fx = new ApprovalTestFixture();

        Assert.False(fx.Approval.IsClearedToProceed(Guid.NewGuid()));
        Assert.False(fx.Approval.IsClearedToProceed(Guid.Empty));
    }

    [Fact]
    public void IsClearedToProceed_FollowsDecisionsEndToEnd()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        Assert.False(fx.Approval.IsClearedToProceed(id));

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Defer, reason: "Waiting on a dependency."));
        Assert.False(fx.Approval.IsClearedToProceed(id));

        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));
        Assert.True(fx.Approval.IsClearedToProceed(id));
    }

    [Fact]
    public void IsClearedToProceed_RejectedRequest_ReturnsFalse()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Reject, reason: "Out of scope."));

        Assert.False(fx.Approval.IsClearedToProceed(id));
    }
}
