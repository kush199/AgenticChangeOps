using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>US1: an Accepted latest design lets the proposal through exactly as before.</summary>
public sealed class ProposalDecisionAcceptedTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task AnAcceptedLatestDesign_StoresTheProposalAsBefore()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.SetDecision(id, ProposalDesignVerdict.Accepted);

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Proposal!.Version);
        Assert.Equal(1, result.Proposal.DesignVersion);
        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Single(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(1, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }

    [Fact]
    public async Task ASecondRequestOnTheSameAcceptedDesign_StoresVersionTwo()
    {
        var id = _f.ApproveWithDrafts();

        await _f.Proposing.ProposeAsync(id);
        var second = await _f.Proposing.ProposeAsync(id);

        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Proposal!.Version);
    }

    [Fact]
    public async Task TheGeneratorIsGivenTheDesignWithItsVerdict()
    {
        var id = _f.ApproveWithDrafts();

        await _f.Proposing.ProposeAsync(id);

        Assert.Equal(ProposalDesignVerdict.Accepted, _f.Generator.LastDesign!.Decision);
    }
}
