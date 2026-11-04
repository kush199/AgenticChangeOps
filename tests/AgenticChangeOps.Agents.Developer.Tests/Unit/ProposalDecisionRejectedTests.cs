using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>US3: a rejected latest design blocks the proposal, with its own reason.</summary>
public sealed class ProposalDecisionRejectedTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task ARejectedLatestDesign_IsRefused_WithAReasonDifferentFromUndecided()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.SetDecision(id, ProposalDesignVerdict.None);
        var undecided = Assert.Single((await _f.Proposing.ProposeAsync(id)).Errors);
        _f.Basis.SetDecision(id, ProposalDesignVerdict.Rejected);

        var result = await _f.Proposing.ProposeAsync(id);

        var rejected = Assert.Single(result.Errors);
        Assert.Equal("DesignAcceptance", rejected.FieldName);
        Assert.Contains("was rejected", rejected.Message);
        Assert.NotEqual(undecided.Message, rejected.Message);
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }
}
