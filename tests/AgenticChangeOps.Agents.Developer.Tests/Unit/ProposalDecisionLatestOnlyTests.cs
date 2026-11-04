using AgenticChangeOps.Agents.Developer.Tests.Fakes;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// US4 at the service: only the verdict carried by the latest design counts. The adapter that picks
/// the verdict of the latest version out of the recorded decisions is covered over HTTP.
/// </summary>
public sealed class ProposalDecisionLatestOnlyTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task ANewerUndecidedDesign_IsRefused_EvenThoughAnEarlierOneWasAccepted()
    {
        var id = _f.ApproveWithDrafts();
        Assert.True((await _f.Proposing.ProposeAsync(id)).IsSuccess);

        _f.Basis.Set(id, new ProposalBasis(
            FakeProposalBasisSource.Requirements(),
            FakeProposalBasisSource.Design(version: 2, requirementsVersion: 1)));
        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("DesignAcceptance", Assert.Single(result.Errors).FieldName);
        Assert.Single(_f.Store.GetAgentOutputs<ChangeProposal>(id));
    }

    [Fact]
    public async Task ANewerAcceptedDesign_Proceeds_AfterARejectedOne()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.SetDecision(id, ProposalDesignVerdict.Rejected);
        Assert.False((await _f.Proposing.ProposeAsync(id)).IsSuccess);

        _f.Basis.Set(id, FakeProposalBasisSource.Current(designVersion: 2, decision: ProposalDesignVerdict.Accepted));
        var result = await _f.Proposing.ProposeAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Proposal!.DesignVersion);
    }
}
