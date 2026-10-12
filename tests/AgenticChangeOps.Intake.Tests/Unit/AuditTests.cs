using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class AuditTests
{
    private static readonly Guid CrId    = new("aaaaaaaa-0000-0000-0000-000000000004");
    private static readonly Guid AuditId = new("bbbbbbbb-0000-0000-0000-000000000004");
    private static readonly DateTime FixedUtc = new(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc);

    private static (ChangeRequestService svc, InMemoryChangeRequestStore store) BuildSut(
        DateTime? now = null, params Guid[] extraIds)
    {
        var store = new InMemoryChangeRequestStore();
        var clock = new FakeSystemClock { Now = now ?? FixedUtc };
        var allIds = new[] { CrId, AuditId }.Concat(extraIds).ToArray();
        var idGen = new FakeIdGenerator(allIds);
        var svc = new ChangeRequestService(store, clock, idGen, new ChangeRequestValidator());
        return (svc, store);
    }

    private static SubmissionRequest ValidRequest() => new(
        "Add retry logic to payment processor",
        "Handle transient failures with exponential backoff.",
        "Morgan Lee",
        "PaymentGatewayAdapter",
        Priority.Critical,
        ChangeType.Bugfix);

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_AfterSubmit_ReturnsOneEntry()
    {
        var (svc, _) = BuildSut();
        var cr = svc.Submit(ValidRequest()).ChangeRequest!;
        var history = svc.GetAuditHistory(cr.ChangeRequestId);
        Assert.NotNull(history);
        Assert.Single(history);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_AfterSubmit_EntryHasSubmittedEventType()
    {
        var (svc, _) = BuildSut();
        var cr = svc.Submit(ValidRequest()).ChangeRequest!;
        var entry = svc.GetAuditHistory(cr.ChangeRequestId)![0];
        Assert.Equal(AuditEventType.Submitted, entry.EventType);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_AfterSubmit_EntryOccurredAtMatchesSubmittedAt()
    {
        var (svc, _) = BuildSut();
        var cr = svc.Submit(ValidRequest()).ChangeRequest!;
        var entry = svc.GetAuditHistory(cr.ChangeRequestId)![0];
        Assert.Equal(cr.SubmittedAt, entry.OccurredAt);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_AfterSubmit_EntryChangeRequestIdMatchesCr()
    {
        var (svc, _) = BuildSut();
        var cr = svc.Submit(ValidRequest()).ChangeRequest!;
        var entry = svc.GetAuditHistory(cr.ChangeRequestId)![0];
        Assert.Equal(cr.ChangeRequestId, entry.ChangeRequestId);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_UnknownId_ReturnsNull()
    {
        var (svc, _) = BuildSut();
        var result = svc.GetAuditHistory(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_GuidEmpty_ReturnsNull()
    {
        var (svc, _) = BuildSut();
        var result = svc.GetAuditHistory(Guid.Empty);
        Assert.Null(result);
    }

    [Fact(Skip = "synthetic")]
    public void GetAuditHistory_MultipleEntriesInjectedDirectly_ReturnedInAscendingOrder()
    {
        // Inject entries directly into the store to validate ordering independently of Submit
        var store = new InMemoryChangeRequestStore();
        var cr = new ChangeRequest(
            CrId, "Patch notification service", "Fix email send failures.", "Riley Brown",
            "NotificationService", Priority.High, ChangeType.Bugfix,
            ChangeRequestStatus.Submitted, FixedUtc);
        store.Save(cr);

        var t1 = FixedUtc.AddSeconds(1);
        var t0 = FixedUtc;
        var t2 = FixedUtc.AddSeconds(2);

        var e1 = new ChangeRequestAuditEntry(Guid.NewGuid(), CrId, AuditEventType.Submitted, t1, null);
        var e2 = new ChangeRequestAuditEntry(Guid.NewGuid(), CrId, AuditEventType.Submitted, t0, null);
        var e3 = new ChangeRequestAuditEntry(Guid.NewGuid(), CrId, AuditEventType.Submitted, t2, null);

        store.SaveAuditEntry(e1);
        store.SaveAuditEntry(e2);
        store.SaveAuditEntry(e3);

        var clock = new FakeSystemClock { Now = FixedUtc };
        var idGen = new FakeIdGenerator(Guid.NewGuid(), Guid.NewGuid());
        var svc = new ChangeRequestService(store, clock, idGen, new ChangeRequestValidator());

        var history = svc.GetAuditHistory(CrId)!;
        Assert.Equal(3, history.Count);
        Assert.True(history[0].OccurredAt <= history[1].OccurredAt);
        Assert.True(history[1].OccurredAt <= history[2].OccurredAt);
    }
}
