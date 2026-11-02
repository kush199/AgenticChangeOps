using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignDecisionRecordTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task Accept_WithNameAndNoReason_StoresAnAcceptedRecordForThatVersion()
    {
        var id = await _f.ApproveAndDesignAsync();
        var design = _f.Store.GetDesigns(id).Single();
        var decidedAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        _f.Clock.Now = decidedAt;

        var result = _f.Deciding.Decide(id, 1, "  Jordan Reed  ", DesignVerdict.Accept, null);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_f.Deciding.GetDecisions(id));
        Assert.Equal(result.Decision, stored);
        Assert.Equal(id, stored.ChangeRequestId);
        Assert.Equal(1, stored.Version);
        Assert.Equal(design.DesignId, stored.DesignId);
        Assert.Equal(DesignDecisionOutcome.Accepted, stored.Outcome);
        Assert.Equal("Jordan Reed", stored.ApproverName);
        Assert.Null(stored.Reason);
        Assert.Equal(decidedAt, stored.DecidedAt);
    }

    [Fact]
    public async Task Accept_WithAReason_KeepsTheTrimmedReason()
    {
        var id = await _f.ApproveAndDesignAsync();

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, "  Looks sound.  ");

        Assert.Equal("Looks sound.", result.Decision!.Reason);
    }

    [Fact]
    public async Task Reject_WithAReason_StoresARejectedRecord()
    {
        var id = await _f.ApproveAndDesignAsync();

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Reject, "Rollback plan is missing.");

        Assert.True(result.IsSuccess);
        Assert.Equal(DesignDecisionOutcome.Rejected, result.Decision!.Outcome);
        Assert.Equal("Rollback plan is missing.", result.Decision.Reason);
    }

    [Fact]
    public async Task ARejectedDesign_DoesNotStopANewDesign_WhichBecomesTheNextUndecidedVersion()
    {
        var id = await _f.ApproveAndDesignAsync();
        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Reject, "Not enough detail.");

        var second = (await _f.Designing.DesignAsync(id)).Draft!;

        Assert.Equal(2, second.Version);
        Assert.Single(_f.Deciding.GetDecisions(id));
        var decided = _f.Deciding.Decide(id, 2, "Jordan Reed", DesignVerdict.Accept, null);
        Assert.True(decided.IsSuccess);
        Assert.Equal(
            new[] { DesignDecisionOutcome.Rejected, DesignDecisionOutcome.Accepted },
            _f.Deciding.GetDecisions(id).Select(d => d.Outcome));
        Assert.Equal(new[] { 1, 2 }, _f.Deciding.GetDecisions(id).Select(d => d.Version));
    }

    [Fact]
    public async Task GetDecisions_ForARequestWithNoDecision_IsEmpty()
    {
        var id = await _f.ApproveAndDesignAsync();

        Assert.Empty(_f.Deciding.GetDecisions(id));
        Assert.Empty(_f.Deciding.GetDecisions(Guid.NewGuid()));
    }
}
