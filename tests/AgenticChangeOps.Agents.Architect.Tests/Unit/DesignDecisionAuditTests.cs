using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignDecisionAuditTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task EachDecision_AddsExactlyOneDesignDecidedEntry_WithTheDocumentedFields()
    {
        var id = await _f.ApproveAndDesignAsync();
        var before = _f.Store.GetAuditEntries(id).Count;
        var at = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        _f.Clock.Now = at;

        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Reject, "Missing rollback.");

        var added = _f.Store.GetAuditEntries(id).Skip(before).ToArray();
        var entry = Assert.Single(added);
        Assert.Equal(AuditEventType.DesignDecided, entry.EventType);
        Assert.Equal(id, entry.ChangeRequestId);
        Assert.Equal("Jordan Reed", entry.ApproverName);
        Assert.Equal("Missing rollback.", entry.Reason);
        Assert.Equal(1, entry.DraftVersion);
        Assert.Equal("Rejected", entry.Details);
        Assert.Equal(at, entry.OccurredAt);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.Null(entry.AgentName);
    }

    [Fact]
    public async Task AnAcceptedDecision_IsAuditedWithItsOutcome_AndNoReason()
    {
        var id = await _f.ApproveAndDesignAsync();

        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        var entry = Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DesignDecided);
        Assert.Equal("Accepted", entry.Details);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task ADecision_NeverUpdatesTheChangeRequest()
    {
        var id = await _f.ApproveAndDesignAsync();
        var store = new DecisionTestStore(_f.Store);
        var service = new DesignDecisionService(store, _f.Clock, _f.Ids, _f.Basis);
        var before = _f.Store.FindById(id);

        service.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);
        service.Decide(id, 1, "Jordan Reed", DesignVerdict.Reject, "Refused as already decided.");

        Assert.Equal(0, store.UpdateCalls);
        Assert.Equal(before, _f.Store.FindById(id));
    }

    [Fact]
    public async Task ADecision_AddsNoAuditEntryOtherThanItsOwn()
    {
        var id = await _f.ApproveAndDesignAsync();
        var before = _f.Store.GetAuditEntries(id);

        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        var after = _f.Store.GetAuditEntries(id);
        Assert.Equal(before, after.Take(before.Count));
        Assert.Equal(before.Count + 1, after.Count);
    }

    [Fact]
    public async Task IfStoringFails_NeitherTheDecisionNorItsAuditEntryIsKept()
    {
        var id = await _f.ApproveAndDesignAsync();
        var store = new DecisionTestStore(_f.Store) { FailDecisionSave = new InvalidOperationException("disk full") };
        var service = new DesignDecisionService(store, _f.Clock, _f.Ids, _f.Basis);

        Assert.Throws<InvalidOperationException>(() => service.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null));

        Assert.Empty(_f.Deciding.GetDecisions(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignDecided));
    }

    [Fact]
    public async Task ARefusedDecision_AddsNoAuditEntry()
    {
        var id = await _f.ApproveAndDesignAsync();
        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);
        var count = _f.Store.GetAuditEntries(id).Count;

        _f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Reject, "Too late.");
        _f.Deciding.Decide(id, 5, "Jordan Reed", DesignVerdict.Accept, null);
        _f.Deciding.Decide(id, 1, " ", DesignVerdict.Accept, null);

        Assert.Equal(count, _f.Store.GetAuditEntries(id).Count);
    }
}
