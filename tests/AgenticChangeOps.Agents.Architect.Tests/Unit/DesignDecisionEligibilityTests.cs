using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignDecisionEligibilityTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public void AnUnknownChangeRequest_IsRefusedAsNotFound()
    {
        var result = _f.Deciding.Decide(Guid.NewGuid(), 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.NotFound, result.Refusal);
        Assert.Contains(result.Errors, e => e.FieldName == "ChangeRequestId");
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Deferred)]
    [InlineData(ChangeRequestStatus.Rejected)]
    public async Task ARequestThatIsNotApproved_IsRefusedAsNotApproved_AndNothingIsStored(ChangeRequestStatus status)
    {
        var id = await _f.ApproveAndDesignAsync();
        _f.SeedStatus(id, status);

        var result = _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.NotApproved, result.Refusal);
        Assert.Contains(result.Errors, e => e.FieldName == "Status");
        Assert.Empty(_f.Deciding.GetDecisions(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignDecided));
    }

    [Fact]
    public async Task TheChecksRunInOrder_StatusBeforeVersion_VersionBeforeOutdated_OutdatedBeforeDecided_DecidedBeforeFields()
    {
        // Not approved wins over an unknown version and bad fields.
        var id = await _f.ApproveAndDesignAsync(designs: 2);
        _f.Deciding.Decide(id, 2, "Jordan Reed", DesignVerdict.Accept, null);
        _f.SeedStatus(id, ChangeRequestStatus.Deferred);
        Assert.Equal(DesignDecisionRefusal.NotApproved, _f.Deciding.Decide(id, 9, " ", DesignVerdict.Reject, null).Refusal);
        _f.SeedStatus(id, ChangeRequestStatus.Approved);

        // An unknown version wins over bad fields.
        Assert.Equal(DesignDecisionRefusal.VersionNotFound, _f.Deciding.Decide(id, 9, " ", DesignVerdict.Reject, null).Refusal);
        // Outdated wins over already decided and bad fields.
        Assert.Equal(DesignDecisionRefusal.Outdated, _f.Deciding.Decide(id, 1, " ", DesignVerdict.Reject, null).Refusal);
        // Already decided wins over bad fields.
        Assert.Equal(DesignDecisionRefusal.AlreadyDecided, _f.Deciding.Decide(id, 2, " ", DesignVerdict.Reject, null).Refusal);
        // Bad fields are reported only for a request that could otherwise be recorded.
        var fresh = await _f.ApproveAndDesignAsync();
        Assert.Equal(DesignDecisionRefusal.Invalid, _f.Deciding.Decide(fresh, 1, " ", DesignVerdict.Reject, null).Refusal);
    }
}
