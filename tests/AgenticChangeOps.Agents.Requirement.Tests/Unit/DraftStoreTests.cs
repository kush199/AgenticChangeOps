using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using Xunit;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

/// <summary>
/// The draft-store behaviour the Requirement agent relies on, reached through the store's
/// agent-neutral output point (moved from the intake store tests by slice 007).
/// </summary>
public sealed class DraftStoreTests
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
}
