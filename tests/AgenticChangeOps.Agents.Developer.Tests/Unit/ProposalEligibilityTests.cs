using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// US3: every ineligible request is refused with its own field, with no model call and nothing
/// stored. Order: existence, status, requirements draft, design draft, design current.
/// </summary>
public sealed class ProposalEligibilityTests
{
    private readonly ProposalTestFixture _f = new();

    private void AssertNothingHappened(Guid id)
    {
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ToolCalled));
    }

    [Fact]
    public async Task AnUnknownChangeRequest_IsReportedOnTheIdField()
    {
        var result = await _f.Proposing.ProposeAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("ChangeRequestId", Assert.Single(result.Errors).FieldName);
        Assert.Equal(0, _f.Generator.CallCount);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Deferred)]
    public async Task AnyStatusButApproved_IsRefused_AndTheBasisIsNotConsulted(ChangeRequestStatus status)
    {
        var id = _f.SubmitSynthetic();
        _f.Basis.Set(id, FakeProposalBasisSource.Current());
        _f.SeedStatus(id, status);

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Status", Assert.Single(result.Errors).FieldName);
        Assert.Equal(0, _f.Basis.CallCount);
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task NoRequirementsDraft_IsRefused_OnTheRequirementsField()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(null, null));

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("RequirementsDraft", Assert.Single(result.Errors).FieldName);
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task NoDesignDraft_IsRefused_OnTheDesignField()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(FakeProposalBasisSource.Requirements(), null));

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("DesignDraft", Assert.Single(result.Errors).FieldName);
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task ADesignBasedOnOlderRequirements_IsRefused_OnADistinctField()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(
            FakeProposalBasisSource.Requirements(version: 3),
            FakeProposalBasisSource.Design(version: 1, requirementsVersion: 2)));

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("DesignDraftVersion", Assert.Single(result.Errors).FieldName);
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task TheMessages_AreFixedText_WithNoDraftContent()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(
            FakeProposalBasisSource.Requirements(3), FakeProposalBasisSource.Design(1, 2)));

        var result = await _f.Proposing.ProposeAsync(id);

        var message = Assert.Single(result.Errors).Message;
        Assert.DoesNotContain("report", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("portal", message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- clarification Q1: the drafts are checked again just before storing ----

    [Fact]
    public async Task ANewerRequirementsDraft_SavedWhileGenerating_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithDrafts(requirementsVersion: 1, designVersion: 1);
        _f.Generator.Behavior = _ =>
        {
            _f.Basis.Set(id, new ProposalBasis(
                FakeProposalBasisSource.Requirements(2), FakeProposalBasisSource.Design(1, 1)));
            return Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());
        };

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("DesignDraftVersion", Assert.Single(result.Errors).FieldName);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ToolCalled));
    }

    [Fact]
    public async Task ANewerDesignDraft_SavedWhileGenerating_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithDrafts(requirementsVersion: 1, designVersion: 1);
        _f.Generator.Behavior = _ =>
        {
            _f.Basis.Set(id, FakeProposalBasisSource.Current(requirementsVersion: 1, designVersion: 2));
            return Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());
        };

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("DesignDraftVersion", Assert.Single(result.Errors).FieldName);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
    }

    [Fact]
    public async Task TheStatusChanging_WhileGenerating_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = _ =>
        {
            _f.SeedStatus(id, ChangeRequestStatus.Deferred);
            return Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());
        };

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("Status", Assert.Single(result.Errors).FieldName);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
    }
}
