using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// US6: the decision check comes last. With the design undecided, every existing refusal still
/// wins and keeps its field.
/// </summary>
public sealed class ProposalDecisionOrderTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task UnknownRequest_StillReportsTheIdField()
    {
        var result = await _f.Proposing.ProposeAsync(Guid.NewGuid());

        Assert.Equal("ChangeRequestId", Assert.Single(result.Errors).FieldName);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Deferred)]
    public async Task NotApproved_StillWins_OverAnUndecidedDesign(ChangeRequestStatus status)
    {
        var id = _f.SubmitSynthetic();
        _f.Basis.Set(id, FakeProposalBasisSource.Current(decision: ProposalDesignVerdict.None));
        _f.SeedStatus(id, status);

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("Status", Assert.Single(result.Errors).FieldName);
        Assert.Equal(0, _f.Basis.CallCount);
    }

    [Fact]
    public async Task NoRequirementsDraft_StillWins()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(null, FakeProposalBasisSource.Design()));

        Assert.Equal("RequirementsDraft", Assert.Single((await _f.Proposing.ProposeAsync(id)).Errors).FieldName);
    }

    [Fact]
    public async Task NoDesignDraft_StillWins()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(FakeProposalBasisSource.Requirements(), null));

        Assert.Equal("DesignDraft", Assert.Single((await _f.Proposing.ProposeAsync(id)).Errors).FieldName);
    }

    [Theory]
    [InlineData(ProposalDesignVerdict.None)]
    [InlineData(ProposalDesignVerdict.Rejected)]
    public async Task ADesignBasedOnOlderRequirements_StillWins_OverTheDecision(ProposalDesignVerdict verdict)
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.Set(id, new ProposalBasis(
            FakeProposalBasisSource.Requirements(version: 3),
            FakeProposalBasisSource.Design(version: 1, requirementsVersion: 2, decision: verdict)));

        Assert.Equal("DesignDraftVersion", Assert.Single((await _f.Proposing.ProposeAsync(id)).Errors).FieldName);
    }
}
