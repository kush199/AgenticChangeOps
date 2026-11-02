using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignDecisionFinalityTests
{
    private readonly DesigningTestFixture _f = new();

    private void AssertStored(Guid id, int decisions, int audits)
    {
        Assert.Equal(decisions, _f.Deciding.GetDecisions(id).Count);
        Assert.Equal(audits, _f.AuditCount(id, AuditEventType.DesignDecided));
    }

    [Theory]
    [InlineData(DesignVerdict.Accept)]
    [InlineData(DesignVerdict.Reject)]
    public async Task ASecondDecision_OnADecidedVersion_IsRefusedAsAlreadyDecided_AndTheFirstIsUnchanged(DesignVerdict second)
    {
        var id = await _f.ApproveAndDesignAsync();
        var first = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null).Decision!;

        var result = _f.Deciding.Decide(id, 1, "Casey Lin", second, "Changed my mind.");

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignDecisionRefusal.AlreadyDecided, result.Refusal);
        Assert.Equal(first, Assert.Single(_f.Deciding.GetDecisions(id)));
        AssertStored(id, decisions: 1, audits: 1);
    }

    [Fact]
    public async Task ARejectedVersion_StaysRejected_AndCannotBeAccepted()
    {
        var id = await _f.ApproveAndDesignAsync();
        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Reject, "No.");

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.AlreadyDecided, result.Refusal);
        Assert.Equal(DesignDecisionOutcome.Rejected, Assert.Single(_f.Deciding.GetDecisions(id)).Outcome);
    }

    [Fact]
    public async Task ADecision_OnAnOlderVersion_IsRefusedAsOutdated_AndNothingIsStored()
    {
        var id = await _f.ApproveAndDesignAsync(designs: 2);

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.Outdated, result.Refusal);
        AssertStored(id, 0, 0);
    }

    [Fact]
    public async Task DecidingTheLatest_NeverChangesAnOlderVersion()
    {
        var id = await _f.ApproveAndDesignAsync(designs: 2);

        _f.Deciding.Decide(id, 2, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(new[] { 2 }, _f.Deciding.GetDecisions(id).Select(d => d.Version));
    }

    [Fact]
    public async Task TheLatestDesign_BasedOnOlderRequirements_IsRefusedAsOutdated()
    {
        var id = await _f.ApproveAndDesignAsync(basisVersion: 1);
        _f.Basis.Set(id, FakeRequirementsBasisSource.Basis(2));

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.Outdated, result.Refusal);
        AssertStored(id, 0, 0);
    }

    [Fact]
    public async Task ADesignRedrawnOnTheNewRequirements_CanBeDecided()
    {
        var id = await _f.ApproveAndDesignAsync(basisVersion: 1);
        _f.Basis.Set(id, FakeRequirementsBasisSource.Basis(2));
        await _f.Designing.DesignAsync(id);

        Assert.Equal(DesignDecisionRefusal.Outdated, _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null).Refusal);
        Assert.True(_f.Deciding.Decide(id, 2, "Jordan Reed", DesignVerdict.Accept, null).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public async Task AVersionThatDoesNotExist_IsRefusedAsVersionNotFound(int version)
    {
        var id = await _f.ApproveAndDesignAsync();

        var result = _f.Deciding.Decide(id, version, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.VersionNotFound, result.Refusal);
        AssertStored(id, 0, 0);
    }

    [Fact]
    public void ARequestWithNoDesign_HasNoVersionToDecide()
    {
        var id = _f.ApproveWithBasis();

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.VersionNotFound, result.Refusal);
        AssertStored(id, 0, 0);
    }
}
