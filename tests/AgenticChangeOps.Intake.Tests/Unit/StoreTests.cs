using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class StoreTests
{
    private static ChangeRequest MakeChangeRequest(Guid? id = null) => new(
        ChangeRequestId: id ?? Guid.NewGuid(),
        Title: "Refactor scheduler",
        Description: "Decouple scheduler from job runner.",
        Requester: "Quinn Adams",
        AffectedSystem: "SchedulerService",
        Priority: Priority.Medium,
        ChangeType: ChangeType.Feature,
        Status: ChangeRequestStatus.Submitted,
        SubmittedAt: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Save_DuplicateId_ThrowsInvalidOperationException()
    {
        var store = new InMemoryChangeRequestStore();
        var cr = MakeChangeRequest(Guid.NewGuid());
        store.Save(cr);
        Assert.Throws<InvalidOperationException>(() => store.Save(cr));
    }

    [Fact]
    public void SaveAuditEntry_WithNonNullDetails_StoresProperly()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.Save(MakeChangeRequest(id));
        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: Guid.NewGuid(),
            ChangeRequestId: id,
            EventType: AuditEventType.Submitted,
            OccurredAt: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            Details: "Submitted via automated intake test.");
        store.SaveAuditEntry(entry);
        var entries = store.GetAuditEntries(id);
        Assert.Single(entries);
        Assert.Equal(entry, entries[0]); // triggers record Equals for all fields
        Assert.Equal("Submitted via automated intake test.", entries[0].Details);
    }

    [Fact]
    public void GetAuditEntries_UnknownId_ReturnsEmptyList()
    {
        var store = new InMemoryChangeRequestStore();
        var result = store.GetAuditEntries(Guid.NewGuid());
        Assert.Empty(result);
    }

    // Smoke tests for production infrastructure wrappers
    // (non-deterministic calls; verified to return valid values)

    [Fact]
    public void GuidIdGenerator_NewId_ReturnsNonEmptyGuid()
    {
        var gen = new GuidIdGenerator();
        var id = gen.NewId();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void SystemClock_UtcNow_ReturnsUtcKindDateTime()
    {
        var clock = new SystemClock();
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }
}
