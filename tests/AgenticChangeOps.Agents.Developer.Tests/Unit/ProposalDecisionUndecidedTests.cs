using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>US2: a latest design nobody has decided blocks the proposal before the model.</summary>
public sealed class ProposalDecisionUndecidedTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task AnUndecidedLatestDesign_IsRefused_OnTheAcceptanceField_WithNothingDone()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.SetDecision(id, ProposalDesignVerdict.None);
        var statusBefore = _f.Store.FindById(id)!.Status;

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("DesignAcceptance", error.FieldName);
        Assert.Contains("not been decided", error.Message);
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ToolCalled));
        Assert.Equal(statusBefore, _f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task TheMessage_NamesOnlyTheId_AndNoDraftContent()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.SetDecision(id, ProposalDesignVerdict.None);

        var message = Assert.Single((await _f.Proposing.ProposeAsync(id)).Errors).Message;

        Assert.Contains(id.ToString(), message);
        Assert.DoesNotContain("report hour", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("portal settings", message, StringComparison.OrdinalIgnoreCase);
    }
}
