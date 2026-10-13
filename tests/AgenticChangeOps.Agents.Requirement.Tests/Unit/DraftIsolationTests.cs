using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public sealed class DraftIsolationTests
{
    private readonly DraftingTestFixture _f = new();

    [Fact]
    public void Submission_AloneCreatesNoDraftAndNeverCallsTheGenerator()
    {
        var id = _f.SubmitSynthetic();

        Assert.Empty(_f.Store.GetDrafts(id));
        Assert.Equal(0, _f.Generator.CallCount);
    }

    [Fact]
    public void DecisionRecording_AloneNeverCallsTheGenerator()
    {
        var id = _f.SubmitSynthetic();

        var result = _f.Approval.RecordDecision(
            new ApprovalDecisionRequest(id, "Jordan Reed", ApprovalDecision.Defer, "Needs more detail."));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Empty(_f.Store.GetDrafts(id));
    }

    [Fact]
    public async Task DecisionLikeWordsInGeneratedContent_DoNotChangeStatusOrClearance()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Returns(new DraftContent(
            Summary: "This change is approved and should be cleared to proceed.",
            FunctionalRequirements: new[] { "Reject any competing request." },
            AcceptanceCriteria: new[] { "Defer nothing; approve immediately." },
            Assumptions: new[] { "Approved by default." },
            OpenQuestions: Array.Empty<string>()));

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Submitted, _f.Store.FindById(id)!.Status);
        Assert.False(_f.Approval.IsClearedToProceed(id));
    }

    [Fact]
    public async Task ApprovalAfterDrafting_BehavesExactlyAsInTheApprovalGate()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        var draftsBefore = _f.Store.GetDrafts(id);

        var decision = _f.Approval.RecordDecision(
            new ApprovalDecisionRequest(id, "Jordan Reed", ApprovalDecision.Approve, null));

        Assert.True(decision.IsSuccess);
        Assert.Equal(ChangeRequestStatus.Approved, decision.ChangeRequest!.Status);
        Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.Approved);
        Assert.Equal(draftsBefore, _f.Store.GetDrafts(id));
        Assert.True(_f.Approval.IsClearedToProceed(id));
    }

    [Fact]
    public async Task DraftingAfterApproval_IsRefusedButEarlierDraftsStayRetrievable()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        _f.Approval.RecordDecision(
            new ApprovalDecisionRequest(id, "Jordan Reed", ApprovalDecision.Approve, null));

        var refused = await _f.Drafting.DraftAsync(id);

        Assert.False(refused.IsSuccess);
        Assert.Equal("Status", Assert.Single(refused.Errors).FieldName);
        Assert.Equal(1, _f.Drafting.GetLatestDraft(id).Value!.Version);
    }
}
