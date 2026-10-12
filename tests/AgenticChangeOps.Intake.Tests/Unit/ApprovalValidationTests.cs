using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class ApprovalValidationTests
{
    /// <summary>
    /// Captures the observable state of one request so a failed decision can be proven to
    /// have changed nothing: status, audit history, and ids consumed from the generator.
    /// </summary>
    private sealed record Snapshot(
        ChangeRequest? Request,
        IReadOnlyList<ChangeRequestAuditEntry>? History,
        int IdsIssued);

    private static Snapshot Capture(ApprovalTestFixture fx, Guid id) =>
        new(fx.Intake.GetById(id), fx.Intake.GetAuditHistory(id), fx.Ids.IssuedCount);

    private static void AssertUnchanged(ApprovalTestFixture fx, Guid id, Snapshot before)
    {
        var after = Capture(fx, id);
        Assert.Equal(before.Request, after.Request);
        Assert.Equal(before.IdsIssued, after.IdsIssued);
        Assert.Equal(before.History is null, after.History is null);
        if (before.History is not null)
            Assert.Equal(before.History, after.History);
    }

    // ---- unknown ChangeRequestId ----

    [Fact(Skip = "synthetic")]
    public void RecordDecision_UnknownId_ReturnsChangeRequestIdErrorAndStoresNothing()
    {
        var fx = new ApprovalTestFixture();
        var unknown = Guid.NewGuid();
        var before = Capture(fx, unknown);

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(unknown, ApprovalDecision.Approve));

        Assert.False(result.IsSuccess);
        Assert.Null(result.ChangeRequest);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ChangeRequestId", error.FieldName);
        Assert.Contains(unknown.ToString(), error.Message);
        Assert.Null(fx.Intake.GetAuditHistory(unknown));
        AssertUnchanged(fx, unknown, before);
    }

    // ---- approver name ----

    [Theory(Skip = "synthetic")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordDecision_MissingApprover_ReturnsApproverNameError(string? approver)
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Approve, approver: approver));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ApproverName", error.FieldName);
        Assert.Equal(ChangeRequestStatus.Submitted, fx.Intake.GetById(id)!.Status);
        Assert.Single(fx.Intake.GetAuditHistory(id)!);
        AssertUnchanged(fx, id, before);
    }

    // ---- reason rules ----

    [Theory(Skip = "synthetic")]
    [InlineData(ApprovalDecision.Reject, null)]
    [InlineData(ApprovalDecision.Reject, "")]
    [InlineData(ApprovalDecision.Reject, "   ")]
    [InlineData(ApprovalDecision.Defer, null)]
    [InlineData(ApprovalDecision.Defer, "")]
    [InlineData(ApprovalDecision.Defer, "   ")]
    public void RecordDecision_MissingReasonForRejectOrDefer_ReturnsReasonError(
        ApprovalDecision decision, string? reason)
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, decision, reason: reason));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Reason", error.FieldName);
        Assert.Contains(decision.ToString(), error.Message);
        AssertUnchanged(fx, id, before);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_ApproveWithWhitespaceReason_SucceedsAndStoresEmptyReason()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Approve, reason: "    "));

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, fx.Intake.GetAuditHistory(id)![1].Reason);
    }

    // ---- final / ineligible status ----

    [Theory(Skip = "synthetic")]
    [InlineData(ApprovalDecision.Approve, ApprovalDecision.Approve)]
    [InlineData(ApprovalDecision.Approve, ApprovalDecision.Reject)]
    [InlineData(ApprovalDecision.Approve, ApprovalDecision.Defer)]
    [InlineData(ApprovalDecision.Reject, ApprovalDecision.Approve)]
    [InlineData(ApprovalDecision.Reject, ApprovalDecision.Reject)]
    [InlineData(ApprovalDecision.Reject, ApprovalDecision.Defer)]
    public void RecordDecision_RequestInFinalState_ReturnsFinalStateErrorAndChangesNothing(
        ApprovalDecision firstDecision, ApprovalDecision secondDecision)
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, firstDecision, reason: "Initial synthetic decision."));
        var finalStatus = fx.Intake.GetById(id)!.Status;
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, secondDecision, reason: "Second synthetic decision."));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Contains("final", error.Message);
        Assert.Contains(finalStatus.ToString(), error.Message);
        Assert.Equal(finalStatus, fx.Intake.GetById(id)!.Status);
        AssertUnchanged(fx, id, before);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_RequestUnderReview_ReturnsNotEligibleError()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        fx.SeedStatus(id, ChangeRequestStatus.UnderReview);
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Approve));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Contains("not eligible", error.Message);
        Assert.Contains("UnderReview", error.Message);
        AssertUnchanged(fx, id, before);
    }

    // ---- unrecognised decision, collect-all, boundaries ----

    [Fact(Skip = "synthetic")]
    public void RecordDecision_UndefinedDecisionValue_ReturnsDecisionErrorListingAcceptedValues()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, (ApprovalDecision)99));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Decision", error.FieldName);
        Assert.Contains("Approve, Reject, Defer", error.Message);
        AssertUnchanged(fx, id, before);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_MultipleProblems_ReturnsOneErrorPerFailingField()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(
            ApprovalTestFixture.Decide(id, ApprovalDecision.Defer, approver: "  ", reason: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.FieldName == "ApproverName");
        Assert.Contains(result.Errors, e => e.FieldName == "Reason");
        AssertUnchanged(fx, id, before);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_ApproverNameAtLimit_Succeeds()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Approve, approver: new string('a', 100)));

        Assert.True(result.IsSuccess);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_ApproverNameOverLimit_ReturnsLengthError()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Approve, approver: new string('a', 101)));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ApproverName", error.FieldName);
        Assert.Contains("must not exceed 100 characters", error.Message);
        AssertUnchanged(fx, id, before);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_ReasonAtLimit_Succeeds()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Reject, reason: new string('r', 1000)));

        Assert.True(result.IsSuccess);
    }

    [Fact(Skip = "synthetic")]
    public void RecordDecision_ReasonOverLimit_ReturnsLengthError()
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var before = Capture(fx, id);

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(
            id, ApprovalDecision.Reject, reason: new string('r', 1001)));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Reason", error.FieldName);
        Assert.Contains("must not exceed 1000 characters", error.Message);
        AssertUnchanged(fx, id, before);
    }

    // ---- no external / LLM dependency (FR-014) ----

    [Fact(Skip = "synthetic")]
    public void ProductionLibrary_HasNoPackageReferences()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticChangeOps.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var csproj = Path.Combine(dir!.FullName,
            "src", "AgenticChangeOps.Intake", "AgenticChangeOps.Intake.csproj");
        var content = File.ReadAllText(csproj);

        Assert.DoesNotContain("PackageReference", content);
        Assert.DoesNotContain("ProjectReference", content);
    }
}
