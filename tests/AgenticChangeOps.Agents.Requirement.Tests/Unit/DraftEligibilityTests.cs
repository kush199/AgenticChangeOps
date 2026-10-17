using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public sealed class DraftEligibilityTests
{
    private readonly DraftingTestFixture _f = new();

    [Fact]
    public async Task Draft_UnknownId_ReturnsNotFoundErrorNamingTheId()
    {
        var unknown = Guid.NewGuid();

        var result = await _f.Drafting.DraftAsync(unknown);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ChangeRequestId", error.FieldName);
        Assert.Contains(unknown.ToString(), error.Message);
        Assert.Equal(0, _f.Generator.CallCount);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    public async Task Draft_NonDraftableStatus_ReturnsStatusErrorNamingTheStatus(ChangeRequestStatus status)
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, status);

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Contains(status.ToString(), error.Message);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    public async Task Draft_RefusedRequest_NeverCallsGeneratorAndStoresNothing(ChangeRequestStatus status)
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, status);
        var auditBefore = _f.Store.GetAuditEntries(id);

        await _f.Drafting.DraftAsync(id);

        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Empty(_f.Store.GetDrafts(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
    }


    [Fact]
    public async Task Draft_ApprovedWithNoDraft_IsAcceptedAsVersionOne()
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, ChangeRequestStatus.Approved);

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Draft!.Version);
        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Single(_f.Store.GetDrafts(id));
        var entry = _f.Store.GetAuditEntries(id).Last();
        Assert.Equal(AuditEventType.DraftGenerated, entry.EventType);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task Draft_ApprovedWithADraft_IsRefusedWithTheUnchangedStatusMessage()
    {
        var id = _f.SubmitSynthetic();
        Assert.True((await _f.Drafting.DraftAsync(id)).IsSuccess);
        _f.SeedStatus(id, ChangeRequestStatus.Approved);
        var auditBefore = _f.Store.GetAuditEntries(id);

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Equal(
            $"Change request {id} is in status Approved and cannot be drafted; " +
            "only Submitted or Deferred requests can be drafted.",
            error.Message);
        Assert.Null(result.FailureCategory);
        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Single(_f.Store.GetDrafts(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
    }

    [Fact]
    public async Task Draft_RequestBecomesApprovedDuringGeneration_KeepsTheDraftAsVersionOne()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = (request, _) =>
        {
            _f.Store.Update(request with { Status = ChangeRequestStatus.Approved });
            return Task.FromResult<DraftContent?>(FakeDraftGenerator.ValidContent());
        };

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Draft!.Version);
        Assert.Single(_f.Store.GetDrafts(id));
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task Draft_ApprovedAndADraftArrivesDuringGeneration_IsRefusedAndConsumesNoVersion()
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, ChangeRequestStatus.Approved);
        _f.Generator.Behavior = async (_, cancellationToken) =>
        {
            // A second draft request finishes while this one is still generating.
            _f.Generator.Behavior = (_, _) => Task.FromResult<DraftContent?>(FakeDraftGenerator.ValidContent());
            await _f.Drafting.DraftAsync(id, cancellationToken);
            return FakeDraftGenerator.ValidContent();
        };

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        var stored = Assert.Single(_f.Store.GetDrafts(id));
        Assert.Equal(1, stored.Version);
        Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DraftGenerated);
    }
}
