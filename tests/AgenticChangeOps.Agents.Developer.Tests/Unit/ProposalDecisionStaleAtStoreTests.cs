using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// US5: if the latest design stops being the accepted one while the proposal is prepared, the
/// proposal is refused as outdated (not as a decision refusal) and nothing is stored.
/// </summary>
public sealed class ProposalDecisionStaleAtStoreTests
{
    private readonly ProposalTestFixture _f = new();

    private async Task<(Guid Id, ProposalResult Result)> RunWithChangeDuringGeneration(ProposalBasis changed)
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = _ =>
        {
            _f.Basis.Set(id, changed);
            return Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());
        };
        return (id, await _f.Proposing.ProposeAsync(id));
    }

    private void AssertOutdatedAndNothingStored(Guid id, ProposalResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal("DesignDraftVersion", Assert.Single(result.Errors).FieldName);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }

    [Fact]
    public async Task ANewerUndecidedDesign_AppearingDuringGeneration_IsOutdated()
    {
        var changed = new ProposalBasis(FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(version: 2));

        var (id, result) = await RunWithChangeDuringGeneration(changed);

        AssertOutdatedAndNothingStored(id, result);
    }

    [Fact]
    public async Task ANewerAcceptedDesign_AppearingDuringGeneration_IsStillOutdated()
    {
        var (id, result) = await RunWithChangeDuringGeneration(
            FakeProposalBasisSource.Current(designVersion: 2, decision: ProposalDesignVerdict.Accepted));

        AssertOutdatedAndNothingStored(id, result);
    }

    [Fact]
    public async Task TheLatestDesignNoLongerAccepted_DuringGeneration_IsOutdated()
    {
        var (id, result) = await RunWithChangeDuringGeneration(
            FakeProposalBasisSource.Current(decision: ProposalDesignVerdict.Rejected));

        AssertOutdatedAndNothingStored(id, result);
    }

    [Fact]
    public async Task ProposalsStoredEarlier_AreUnchanged_ByALaterNewerDesign()
    {
        var id = _f.ApproveWithDrafts();
        var first = (await _f.Proposing.ProposeAsync(id)).Proposal!;

        _f.Basis.Set(id, new ProposalBasis(FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(version: 2)));
        var refused = await _f.Proposing.ProposeAsync(id);

        Assert.False(refused.IsSuccess);
        Assert.Equal(first, Assert.Single(_f.Store.GetAgentOutputs<ChangeProposal>(id)));
        Assert.Equal(first, _f.Proposing.GetLatestProposal(id).Value);
    }
}
