using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Persistence.Tests.Support;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>
/// D2: a failure between the first and second insert of a combined write leaves nothing behind.
/// The failure is injected by a trigger on the audit table, created on a separate connection.
/// </summary>
public sealed class AtomicityTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={_database.FilePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void BlockAuditInserts() =>
        Execute("CREATE TRIGGER block_audit BEFORE INSERT ON audit_entries BEGIN SELECT RAISE(ABORT, 'blocked'); END;");

    private void AllowAuditInserts() => Execute("DROP TRIGGER block_audit;");

    [Fact]
    public void SaveAgentOutput_WhenTheAuditInsertFails_StoresNeither_AndWorksAgainAfterwards()
    {
        using var store = new SqliteChangeRequestStore(_database.Options);
        var request = SampleRecords.Request();
        store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));
        BlockAuditInserts();
        var id = request.ChangeRequestId;
        var output = SampleRecords.Output(id, 1);

        Assert.ThrowsAny<Exception>(() =>
            store.SaveAgentOutput(output, SampleRecords.Entry(id, AuditEventType.DesignGenerated, minute: 2)));

        Assert.Empty(store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Single(store.GetAuditEntries(id));

        AllowAuditInserts();
        store.SaveAgentOutput(output, SampleRecords.Entry(id, AuditEventType.DesignGenerated, minute: 3));
        Assert.Single(store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Equal(2, store.GetAuditEntries(id).Count);
    }

    [Fact]
    public void SaveWithAudit_WhenTheAuditInsertFails_StoresNeither_AndWorksAgainAfterwards()
    {
        using var store = new SqliteChangeRequestStore(_database.Options);
        var seed = SampleRecords.Request();
        store.SaveWithAudit(seed, SampleRecords.Entry(seed.ChangeRequestId));
        BlockAuditInserts();
        var request = SampleRecords.Request();
        var entry = SampleRecords.Entry(request.ChangeRequestId);

        Assert.ThrowsAny<Exception>(() => store.SaveWithAudit(request, entry));

        Assert.Null(store.FindById(request.ChangeRequestId));
        Assert.Empty(store.GetAuditEntries(request.ChangeRequestId));

        AllowAuditInserts();
        store.SaveWithAudit(request, entry);
        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
    }

    [Fact]
    public void UpdateWithAudit_WhenTheAuditInsertFails_LeavesTheChangeRequestUnchanged_AndWorksAgainAfterwards()
    {
        using var store = new SqliteChangeRequestStore(_database.Options);
        var request = SampleRecords.Request();
        store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));
        BlockAuditInserts();
        var approved = request with { Status = ChangeRequestStatus.Approved };
        var entry = SampleRecords.Entry(request.ChangeRequestId, AuditEventType.Approved, minute: 2);

        Assert.ThrowsAny<Exception>(() => store.UpdateWithAudit(approved, entry));

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
        Assert.Single(store.GetAuditEntries(request.ChangeRequestId));

        AllowAuditInserts();
        store.UpdateWithAudit(approved, entry);
        RecordAssert.Same(approved, store.FindById(request.ChangeRequestId));
        Assert.Equal(2, store.GetAuditEntries(request.ChangeRequestId).Count);
    }
}
