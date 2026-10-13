using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignEligibilityTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task Design_UnknownId_ReturnsNotFoundErrorNamingTheId()
    {
        var unknown = Guid.NewGuid();

        var result = await _f.Designing.DesignAsync(unknown);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ChangeRequestId", error.FieldName);
        Assert.Contains(unknown.ToString(), error.Message);
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Null(result.FailureCategory);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Deferred)]
    [InlineData(ChangeRequestStatus.Rejected)]
    public async Task Design_StatusOtherThanApproved_ReturnsStatusErrorNamingTheStatus(ChangeRequestStatus status)
    {
        var id = _f.ApproveWithBasis();
        _f.SeedStatus(id, status);

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Contains(status.ToString(), error.Message);
        Assert.Contains("Approved", error.Message);
        Assert.Null(result.FailureCategory);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Deferred)]
    [InlineData(ChangeRequestStatus.Rejected)]
    public async Task Design_RefusedForStatus_NeverCallsGeneratorOrBasisSourceAndStoresNothing(
        ChangeRequestStatus status)
    {
        var id = _f.ApproveWithBasis();
        _f.SeedStatus(id, status);
        var basisCallsBefore = _f.Basis.CallCount;
        var auditBefore = _f.Store.GetAuditEntries(id);

        await _f.Designing.DesignAsync(id);

        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Equal(basisCallsBefore, _f.Basis.CallCount);
        Assert.Empty(_f.Store.GetDesigns(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
    }

    [Fact]
    public async Task Design_NotApprovedAndNoBasis_ReportsTheStatusOnly()
    {
        var id = _f.SubmitSynthetic();

        var result = await _f.Designing.DesignAsync(id);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
    }

    [Fact]
    public async Task Design_ApprovedWithNoRequirementsDraft_ReturnsRequirementsDraftError()
    {
        var id = _f.SubmitSynthetic();
        _f.Approval.RecordDecision(new AgenticChangeOps.Intake.Models.ApprovalDecisionRequest(
            id, "Jordan Reed", ApprovalDecision.Approve, null));
        var auditBefore = _f.Store.GetAuditEntries(id);

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("RequirementsDraft", error.FieldName);
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Empty(_f.Store.GetDesigns(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
    }

    [Fact]
    public async Task Design_RequestStopsBeingApprovedDuringGeneration_IsRefusedAndConsumesNoVersion()
    {
        var id = _f.ApproveWithBasis();
        _f.Generator.Behavior = (request, _, _) =>
        {
            _f.Store.Update(request with { Status = ChangeRequestStatus.Deferred });
            return Task.FromResult<DesignContent?>(FakeDesignGenerator.ValidContent());
        };

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Status", error.FieldName);
        Assert.Empty(_f.Store.GetDesigns(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignGenerated));

        _f.SeedStatus(id, ChangeRequestStatus.Approved);
        _f.Generator.Behavior = (_, _, _) =>
            Task.FromResult<DesignContent?>(FakeDesignGenerator.ValidContent());
        var retry = await _f.Designing.DesignAsync(id);

        Assert.True(retry.IsSuccess);
        Assert.Equal(1, retry.Draft!.Version);
    }

    [Fact]
    public async Task Design_RequirementsDraftChangesDuringGeneration_IsRefusedAsRequirementsDraftError()
    {
        var id = _f.ApproveWithBasis(basisVersion: 1);
        _f.Generator.Behavior = (_, _, _) =>
        {
            _f.Basis.Set(id, FakeRequirementsBasisSource.Basis(2));
            return Task.FromResult<DesignContent?>(FakeDesignGenerator.ValidContent());
        };

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("RequirementsDraft", error.FieldName);
        Assert.Empty(_f.Store.GetDesigns(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignGenerated));
    }
}
