using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class DraftEligibilityTests
{
    private readonly DraftingTestFixture _f = new();

    [Fact(Skip = "synthetic")]
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

    [Theory(Skip = "synthetic")]
    [InlineData(ChangeRequestStatus.Approved)]
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

    [Theory(Skip = "synthetic")]
    [InlineData(ChangeRequestStatus.Approved)]
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

    [Fact(Skip = "synthetic")]
    public async Task Draft_RequestBecomesApprovedDuringGeneration_IsRefusedAndConsumesNoVersion()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = (request, _) =>
        {
            _f.Store.Update(request with { Status = ChangeRequestStatus.Approved });
            return Task.FromResult<Models.DraftContent?>(FakeDraftGenerator.ValidContent());
        };

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Empty(_f.Store.GetDrafts(id));
        Assert.DoesNotContain(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DraftGenerated);

        _f.SeedStatus(id, ChangeRequestStatus.Submitted);
        _f.Generator.Behavior = (_, _) =>
            Task.FromResult<Models.DraftContent?>(FakeDraftGenerator.ValidContent());
        var retry = await _f.Drafting.DraftAsync(id);

        Assert.True(retry.IsSuccess);
        Assert.Equal(1, retry.Draft!.Version);
    }
}
