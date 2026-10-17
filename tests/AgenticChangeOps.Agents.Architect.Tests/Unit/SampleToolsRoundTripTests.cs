using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// FR-019: the added list round-trips through both stores; with tools off (or for a draft stored
/// before this feature) the stored payload is byte-identical to the one recorded before the change
/// (specs/015-architect-reads-sample-code/baseline/).
/// </summary>
public sealed class SampleToolsRoundTripTests : IDisposable
{
    private static readonly Guid DesignId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid RequestId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid AuditId = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
    private static readonly DateTime At = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "acops-roundtrip-" + Guid.NewGuid().ToString("N"));

    public SampleToolsRoundTripTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static string Baseline(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "specs", "015-architect-reads-sample-code", "baseline", name));

    private static ChangeRequest Request() => new(
        RequestId, "Add synthetic report export", "Add a nightly export of the synthetic widget report.",
        "Sample Requester", "Sample Reporting Service", Priority.High, ChangeType.Feature,
        ChangeRequestStatus.Approved, At);

    private static DesignDraft Draft(IReadOnlyList<string>? files) => new(
        DesignId, RequestId, 1, 1, At, DesignDraftStatus.Draft, DesignDraft.ArchitectAgentName,
        "Summary text.", new[] { "Component A" }, new[] { "Risk A" }, new[] { "Dependency A" },
        new[] { "Rollback A" }, new[] { "Question A" }, files);

    private static ChangeRequestAuditEntry DesignEntry() => new(
        AuditId, RequestId, AuditEventType.DesignGenerated, At, null,
        StatusBefore: ChangeRequestStatus.Approved, StatusAfter: ChangeRequestStatus.Approved,
        AgentName: DesignDraft.ArchitectAgentName, DraftVersion: 1);

    private static ChangeRequestAuditEntry ToolEntry(int n) => new(
        Guid.Parse($"00000000-0000-0000-0000-00000000d{n:D3}"), RequestId, AuditEventType.ToolCalled, At.AddSeconds(-10 + n), null,
        StatusBefore: ChangeRequestStatus.Approved, StatusAfter: ChangeRequestStatus.Approved,
        AgentName: DesignDraft.ArchitectAgentName, ToolName: "read_file", ToolTarget: "orders-service/src/OrderIntake",
        ToolOutcome: "Served", ResultBytes: 321, ElapsedMilliseconds: 17);

    private string DatabasePath => Path.Combine(_folder, "changeops.db");

    private SqliteChangeRequestStore NewSqlite() => new(new SqliteStorageOptions(DatabasePath));

    private string ReadPayload(string table, string where)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False;Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT payload FROM {table} WHERE {where}";
        return (string)command.ExecuteScalar()!;
    }

    private static void Seed(IChangeRequestStore store, DesignDraft draft, params ChangeRequestAuditEntry[] toolEntries)
    {
        store.SaveWithAudit(Request(), new ChangeRequestAuditEntry(
            Guid.Parse("00000000-0000-0000-0000-0000000000e1"), RequestId, AuditEventType.Submitted, At.AddMinutes(-5), null));
        foreach (var entry in toolEntries)
            store.SaveAuditEntry(entry);
        store.SaveAgentOutput(draft, DesignEntry());
    }

    [Fact]
    public void Sqlite_RoundTripsTheFileList_AndTheToolCalledEntries()
    {
        var files = new[] { "orders-service/src/OrderIntake", "orders-service/src/StockCheck" };
        using (var store = NewSqlite())
            Seed(store, Draft(files), ToolEntry(1), ToolEntry(2));

        using var reopened = NewSqlite();
        var design = Assert.Single(reopened.GetAgentOutputs<DesignDraft>(RequestId));
        Assert.Equal(files, design.SampleFilesRead);
        var calls = reopened.GetAuditEntries(RequestId).Where(e => e.EventType == AuditEventType.ToolCalled).ToArray();
        Assert.Equal(2, calls.Length);
        Assert.Equal(("read_file", "orders-service/src/OrderIntake", "Served", 321, 17),
            (calls[0].ToolName, calls[0].ToolTarget, calls[0].ToolOutcome, calls[0].ResultBytes, calls[0].ElapsedMilliseconds));
    }

    [Fact]
    public void Sqlite_RoundTripsAnEmptyList_AsEmpty_NotNull()
    {
        using (var store = NewSqlite())
            Seed(store, Draft(Array.Empty<string>()));

        using var reopened = NewSqlite();
        var design = Assert.Single(reopened.GetAgentOutputs<DesignDraft>(RequestId));
        Assert.NotNull(design.SampleFilesRead);
        Assert.Empty(design.SampleFilesRead!);
    }

    [Fact]
    public void InMemory_RoundTripsTheFileList()
    {
        var store = new InMemoryChangeRequestStore();
        Seed(store, Draft(new[] { "billing-portal/src/InvoiceList" }), ToolEntry(1));

        var design = Assert.Single(store.GetAgentOutputs<DesignDraft>(RequestId));
        Assert.Equal(new[] { "billing-portal/src/InvoiceList" }, design.SampleFilesRead);
        Assert.Single(store.GetAuditEntries(RequestId), e => e.EventType == AuditEventType.ToolCalled);
    }

    [Fact]
    public void ADraftWithNoFileList_IsStoredAsTheByteIdenticalPayloadRecordedBeforeTheChange()
    {
        using (var store = NewSqlite())
            Seed(store, Draft(files: null));

        Assert.Equal(Baseline("stored-design-payload.json"), ReadPayload("agent_outputs", "output_type LIKE '%Design%'"));
    }

    [Fact]
    public void AnAuditEntryOfAnExistingKind_IsStoredAsTheByteIdenticalPayloadRecordedBeforeTheChange()
    {
        using (var store = NewSqlite())
            Seed(store, Draft(files: null));

        Assert.Equal(Baseline("stored-audit-entry.json"), ReadPayload("audit_entries", "payload LIKE '%DesignGenerated%'"));
    }

    [Fact]
    public void AToolCalledPayload_HoldsNoContentMember_AndNoNullMembers()
    {
        using (var store = NewSqlite())
            Seed(store, Draft(new[] { "orders-service/src/OrderIntake" }), ToolEntry(1));

        var payload = ReadPayload("audit_entries", "payload LIKE '%ToolCalled%'");

        Assert.Contains("\"ToolName\":\"read_file\"", payload);
        Assert.Contains("\"ResultBytes\":321", payload);
        Assert.DoesNotContain("Content", payload);
        Assert.DoesNotContain("Automatic", payload);
    }

    [Fact]
    public void ADraftStoredBeforeThisFeature_LoadsWithANullFileList()
    {
        using (var store = NewSqlite())
            Seed(store, Draft(files: null));

        using var reopened = NewSqlite();
        Assert.Null(Assert.Single(reopened.GetAgentOutputs<DesignDraft>(RequestId)).SampleFilesRead);
    }

    [Fact]
    public void TheStorageLayoutVersion_IsStillTwo()
    {
        using (var store = NewSqlite())
            Seed(store, Draft(files: null));

        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False;Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        Assert.Equal(2L, command.ExecuteScalar());
    }
}
