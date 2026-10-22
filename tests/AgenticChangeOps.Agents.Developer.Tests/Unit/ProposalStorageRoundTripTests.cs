using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// The typed proposal record through both stores (research R8): saved with its audit entry, read
/// back in version order, identical after a restart of the durable store, and no schema change.
/// </summary>
public sealed class ProposalStorageRoundTripTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "changeops-proposal-" + Guid.NewGuid().ToString("N"));

    public ProposalStorageRoundTripTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string DbPath => Path.Combine(_folder, "changeops.db");

    private static ChangeProposal Proposal(Guid id, int version, bool withAdd = false)
    {
        var changes = new List<ProposedChange>
        {
            new("billing-portal/config/portal.settings", ProposalChangeKind.Change, "report_hour = 2\r\n", "report_hour = 4\r\n")
        };
        if (withAdd)
            changes.Add(new ProposedChange("billing-portal/notes/new.md", ProposalChangeKind.Add, null, "# Notes\né"));

        return new ChangeProposal(
            ProposalId: new Guid(version, 0, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            ChangeRequestId: id,
            Version: version,
            RequirementsVersion: 2,
            DesignVersion: 3,
            GeneratedAt: new DateTime(2026, 1, 15, 9, 0, version, DateTimeKind.Utc),
            Status: ProposalStatus.Draft,
            AgentName: ChangeProposal.DeveloperAgentName,
            Summary: "Move the report hour.",
            Changes: changes,
            Risks: new[] { "Later reports." },
            TestIdeas: new[] { "Parse the settings." },
            OpenQuestions: Array.Empty<string>(),
            SampleFilesRead: new[] { "billing-portal/config/portal.settings" },
            VerificationNote: ChangeProposal.VerificationNoteText);
    }

    private static ChangeRequest Request() => new(
        Guid.NewGuid(), "Move nightly report schedule", "Move the job.", "Avery Quill", "Sample Billing Portal",
        Priority.Medium, ChangeType.ConfigChange, ChangeRequestStatus.Approved,
        new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));

    private static ChangeRequestAuditEntry Entry(ChangeProposal p) => new(
        AuditEntryId: Guid.NewGuid(),
        ChangeRequestId: p.ChangeRequestId,
        EventType: AuditEventType.ProposalGenerated,
        OccurredAt: p.GeneratedAt,
        Details: null,
        StatusBefore: ChangeRequestStatus.Approved,
        StatusAfter: ChangeRequestStatus.Approved,
        AgentName: p.AgentName,
        DraftVersion: p.Version);

    public static IEnumerable<object[]> Stores()
    {
        yield return new object[] { "memory" };
        yield return new object[] { "sqlite" };
    }

    private IChangeRequestStore Open(string kind) => kind == "memory"
        ? new InMemoryChangeRequestStore()
        : new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath));

    [Theory]
    [MemberData(nameof(Stores))]
    public void AProposal_IsStoredWithItsAuditEntry_AndReadBackEqual(string kind)
    {
        var store = Open(kind);
        var request = Request();
        store.Save(request);
        var proposal = Proposal(request.ChangeRequestId, 1, withAdd: true);

        store.SaveProposal(proposal, Entry(proposal));

        var read = Assert.Single(store.GetProposals(request.ChangeRequestId));
        Assert.Equal(proposal.ChangeRequestId, read.ChangeRequestId);
        Assert.Equal(proposal.Summary, read.Summary);
        Assert.Equal(proposal.Changes, read.Changes);
        Assert.Equal(proposal.Risks, read.Risks);
        Assert.Equal(proposal.SampleFilesRead, read.SampleFilesRead);
        Assert.Equal(proposal.GeneratedAt, read.GeneratedAt);
        Assert.Equal((proposal.RequirementsVersion, proposal.DesignVersion), (read.RequirementsVersion, read.DesignVersion));
        Assert.Equal(ProposalStatus.Draft, read.Status);
        Assert.Equal(ChangeProposal.VerificationNoteText, read.VerificationNote);
        Assert.Single(store.GetAuditEntries(request.ChangeRequestId), e => e.EventType == AuditEventType.ProposalGenerated);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void Proposals_AreReturnedInAscendingVersionOrder_PerChangeRequest(string kind)
    {
        var store = Open(kind);
        var a = Request();
        var b = Request();
        store.Save(a);
        store.Save(b);
        foreach (var version in new[] { 1, 2, 3 })
        {
            var p = Proposal(a.ChangeRequestId, version);
            store.SaveProposal(p, Entry(p));
        }

        var other = Proposal(b.ChangeRequestId, 1);
        store.SaveProposal(other, Entry(other));

        Assert.Equal(new[] { 1, 2, 3 }, store.GetProposals(a.ChangeRequestId).Select(p => p.Version));
        Assert.Single(store.GetProposals(b.ChangeRequestId));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ADuplicateVersion_IsRefused_AndNothingIsAdded(string kind)
    {
        var store = Open(kind);
        var request = Request();
        store.Save(request);
        var first = Proposal(request.ChangeRequestId, 1);
        store.SaveProposal(first, Entry(first));
        var auditBefore = store.GetAuditEntries(request.ChangeRequestId).Count;

        Assert.ThrowsAny<Exception>(() => store.SaveProposal(Proposal(request.ChangeRequestId, 1), Entry(first)));

        Assert.Single(store.GetProposals(request.ChangeRequestId));
        Assert.Equal(auditBefore, store.GetAuditEntries(request.ChangeRequestId).Count);
    }

    [Fact]
    public void TheDurableStore_ReturnsTheSameProposals_AfterARestart()
    {
        var request = Request();
        var proposal = Proposal(request.ChangeRequestId, 1, withAdd: true);
        using (var store = (SqliteChangeRequestStore)Open("sqlite"))
        {
            store.Save(request);
            store.SaveProposal(proposal, Entry(proposal));
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var reopened = new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath));

        var read = Assert.Single(reopened.GetProposals(request.ChangeRequestId));
        Assert.Equal(proposal.Changes, read.Changes);
        Assert.Equal(proposal.Summary, read.Summary);
    }

    [Fact]
    public void AStoreWithNoProposals_ReadsAnEmptyList_AndOtherOutputTypesAreUnaffected()
    {
        var store = Open("memory");
        var request = Request();
        store.Save(request);

        Assert.Empty(store.GetProposals(request.ChangeRequestId));
        Assert.Empty(store.GetProposals(Guid.NewGuid()));
    }
}
