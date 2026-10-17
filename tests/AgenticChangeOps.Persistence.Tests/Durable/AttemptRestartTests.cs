using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Persistence.Tests.Support;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>Attempt rows survive a restart and never touch the other tables.</summary>
public sealed class AttemptRestartTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private long Count(string table)
    {
        using var connection = new SqliteConnection($"Data Source={_database.FilePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static AutomaticAttempt Attempt(Guid id, int failures) =>
        new(id, "DesignDraft", 2, failures, "TimedOut", SampleRecords.Start.AddMinutes(failures).AddTicks(77));

    [Fact]
    public void AttemptRows_AreReadBackEqual_AfterReopening()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        using (var store = new SqliteChangeRequestStore(_database.Options))
        {
            store.SaveAutomaticAttempt(Attempt(a, 1));
            store.SaveAutomaticAttempt(Attempt(b, 2));
            store.SaveAutomaticAttempt(Attempt(a, 3));
        }

        using var reopened = new SqliteChangeRequestStore(_database.Options);

        Assert.Equal(Attempt(a, 3), reopened.GetAutomaticAttempt(a));
        Assert.Equal(Attempt(b, 2), reopened.GetAutomaticAttempt(b));
    }

    [Fact]
    public void ARequestSavedBeforeARestart_IsStillListedInTheSameOrder()
    {
        var ids = new List<Guid>();
        using (var store = new SqliteChangeRequestStore(_database.Options))
        {
            for (var i = 0; i < 4; i++)
            {
                var request = SampleRecords.Request(minute: i);
                ids.Add(request.ChangeRequestId);
                store.Save(request);
            }
        }

        using var reopened = new SqliteChangeRequestStore(_database.Options);

        Assert.Equal(ids, reopened.ListChangeRequestIds().ToList());
    }

    [Fact]
    public void SavingOrClearingARow_NeverTouchesTheOtherTables()
    {
        var request = SampleRecords.Request();
        using var store = new SqliteChangeRequestStore(_database.Options);
        store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));
        store.SaveAgentOutput(
            SampleRecords.Output(request.ChangeRequestId, 1),
            SampleRecords.Entry(request.ChangeRequestId, minute: 1));
        var before = (Count("change_requests"), Count("audit_entries"), Count("agent_outputs"));

        store.SaveAutomaticAttempt(Attempt(request.ChangeRequestId, 1));
        store.SaveAutomaticAttempt(Attempt(request.ChangeRequestId, 2));
        store.ClearAutomaticAttempt(request.ChangeRequestId);

        Assert.Equal(before, (Count("change_requests"), Count("audit_entries"), Count("agent_outputs")));
        Assert.Equal(0, Count("automatic_attempts"));
    }
}
