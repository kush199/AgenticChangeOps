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

    [Fact]
    public void Update_ExistingId_ReplacesStoredRecord()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        var original = MakeChangeRequest(id);
        store.Save(original);

        var updated = original with { Status = ChangeRequestStatus.Approved };
        store.Update(updated);

        Assert.Equal(updated, store.FindById(id));
    }

    [Fact]
    public void Update_UnknownId_ThrowsInvalidOperationException()
    {
        var store = new InMemoryChangeRequestStore();
        var cr = MakeChangeRequest(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => store.Update(cr));
    }

    private static RequirementsDraft MakeDraft(Guid changeRequestId, int version) => new(
        DraftId: Guid.NewGuid(),
        ChangeRequestId: changeRequestId,
        Version: version,
        GeneratedAt: new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
        Status: DraftStatus.Draft,
        AgentName: RequirementsDraft.RequirementAgentName,
        Summary: "Move the nightly report job to a new window.",
        FunctionalRequirements: new[] { "The job runs at the new time." },
        AcceptanceCriteria: new[] { "The report arrives by 06:00." },
        Assumptions: Array.Empty<string>(),
        OpenQuestions: Array.Empty<string>());

    private static ChangeRequestAuditEntry MakeDraftEntry(Guid changeRequestId, int version) => new(
        AuditEntryId: Guid.NewGuid(),
        ChangeRequestId: changeRequestId,
        EventType: AuditEventType.DraftGenerated,
        OccurredAt: new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
        Details: null,
        StatusBefore: ChangeRequestStatus.Submitted,
        StatusAfter: ChangeRequestStatus.Submitted,
        AgentName: RequirementsDraft.RequirementAgentName,
        DraftVersion: version);

    [Fact]
    public void SaveDraft_StoresDraftAndAuditEntryTogether()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.Save(MakeChangeRequest(id));
        var draft = MakeDraft(id, 1);
        var entry = MakeDraftEntry(id, 1);

        store.SaveDraft(draft, entry);

        Assert.Equal(draft, Assert.Single(store.GetDrafts(id)));
        Assert.Equal(entry, Assert.Single(store.GetAuditEntries(id)));
    }

    [Fact]
    public void GetDrafts_ReturnsAscendingVersionOrder()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.Save(MakeChangeRequest(id));
        store.SaveDraft(MakeDraft(id, 1), MakeDraftEntry(id, 1));
        store.SaveDraft(MakeDraft(id, 2), MakeDraftEntry(id, 2));
        store.SaveDraft(MakeDraft(id, 3), MakeDraftEntry(id, 3));

        Assert.Equal(new[] { 1, 2, 3 }, store.GetDrafts(id).Select(d => d.Version));
    }

    [Fact]
    public void GetDrafts_UnknownId_ReturnsEmptyList()
    {
        var store = new InMemoryChangeRequestStore();
        Assert.Empty(store.GetDrafts(Guid.NewGuid()));
    }

    [Fact]
    public void GetDrafts_DraftsOfTwoRequests_DoNotMix()
    {
        var store = new InMemoryChangeRequestStore();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        store.Save(MakeChangeRequest(first));
        store.Save(MakeChangeRequest(second));
        store.SaveDraft(MakeDraft(first, 1), MakeDraftEntry(first, 1));
        store.SaveDraft(MakeDraft(second, 1), MakeDraftEntry(second, 1));

        Assert.Single(store.GetDrafts(first));
        Assert.Single(store.GetDrafts(second));
        Assert.All(store.GetDrafts(first), d => Assert.Equal(first, d.ChangeRequestId));
    }

    [Fact]
    public void SaveDraft_DuplicateVersion_ThrowsAndStoresNeitherItem()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.Save(MakeChangeRequest(id));
        store.SaveDraft(MakeDraft(id, 1), MakeDraftEntry(id, 1));

        Assert.Throws<InvalidOperationException>(() =>
            store.SaveDraft(MakeDraft(id, 1), MakeDraftEntry(id, 1)));

        Assert.Single(store.GetDrafts(id));
        Assert.Single(store.GetAuditEntries(id));
    }

    [Fact]
    public void SaveDraft_MismatchedChangeRequestIds_ThrowsAndStoresNeitherItem()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            store.SaveDraft(MakeDraft(id, 1), MakeDraftEntry(other, 1)));

        Assert.Empty(store.GetDrafts(id));
        Assert.Empty(store.GetAuditEntries(id));
        Assert.Empty(store.GetAuditEntries(other));
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
