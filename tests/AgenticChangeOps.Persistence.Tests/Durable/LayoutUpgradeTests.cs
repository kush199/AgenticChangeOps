using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>Layout 1 to 2: a version-1 file is upgraded in place; anything else unknown is refused untouched.</summary>
public sealed class LayoutUpgradeTests : IDisposable
{
    private const string VersionOneTables = @"
CREATE TABLE change_requests (id TEXT NOT NULL PRIMARY KEY, payload TEXT NOT NULL);
CREATE TABLE audit_entries (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    change_request_id TEXT NOT NULL,
    occurred_at_ticks INTEGER NOT NULL,
    payload TEXT NOT NULL);
CREATE INDEX ix_audit_entries_order ON audit_entries (change_request_id, occurred_at_ticks, seq);
CREATE TABLE agent_outputs (
    change_request_id TEXT NOT NULL,
    output_type TEXT NOT NULL,
    version INTEGER NOT NULL,
    payload TEXT NOT NULL,
    PRIMARY KEY (change_request_id, output_type, version));";

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_database.FilePath};Pooling=False");
        connection.Open();
        return connection;
    }

    private void Run(string sql, Action<SqliteCommand>? parameters = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        parameters?.Invoke(command);
        command.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private (ChangeRequest Request, ChangeRequestAuditEntry Entry) SeedVersionOne()
    {
        var request = SampleRecords.Request();
        var entry = SampleRecords.Entry(request.ChangeRequestId);
        Run(VersionOneTables + "PRAGMA user_version = 1;");
        Run("INSERT INTO change_requests (id, payload) VALUES ($id, $payload);", c =>
        {
            c.Parameters.AddWithValue("$id", request.ChangeRequestId.ToString());
            c.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(request, Json));
        });
        Run("INSERT INTO audit_entries (change_request_id, occurred_at_ticks, payload) VALUES ($id, $ticks, $payload);", c =>
        {
            c.Parameters.AddWithValue("$id", request.ChangeRequestId.ToString());
            c.Parameters.AddWithValue("$ticks", entry.OccurredAt.Ticks);
            c.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(entry, Json));
        });
        return (request, entry);
    }

    [Fact]
    public void AVersionOneFile_IsUpgradedInPlace_KeepsItsData_AndAcceptsAttemptRows()
    {
        var (request, entry) = SeedVersionOne();

        using var store = new SqliteChangeRequestStore(_database.Options);

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
        RecordAssert.Same(entry, Assert.Single(store.GetAuditEntries(request.ChangeRequestId)));
        Assert.Equal(new[] { request.ChangeRequestId }, store.ListChangeRequestIds().ToArray());
        Assert.Equal(2, Scalar("PRAGMA user_version;"));
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'automatic_attempts';"));

        var attempt = new AutomaticAttempt(
            request.ChangeRequestId, "RequirementsDraft", 0, 1, "Unreachable", SampleRecords.Start);
        store.SaveAutomaticAttempt(attempt);
        Assert.Equal(attempt, store.GetAutomaticAttempt(request.ChangeRequestId));
    }

    [Fact]
    public void AVersionOneFileMissingOneOfItsTables_IsRefused_AndLeftByteIdentical()
    {
        Run("CREATE TABLE change_requests (id TEXT NOT NULL PRIMARY KEY, payload TEXT NOT NULL); PRAGMA user_version = 1;");
        var before = Hash(_database.FilePath);

        using var store = new SqliteChangeRequestStore(_database.Options);
        var ex = Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));

        Assert.Equal(StorageFailureCategory.UnsupportedLayout, ex.Category);
        Assert.Equal(before, Hash(_database.FilePath));
    }

    [Fact]
    public void AVersionTwoFileMissingTheAttemptTable_IsRefused_AndLeftByteIdentical()
    {
        Run(VersionOneTables + "PRAGMA user_version = 2;");
        var before = Hash(_database.FilePath);

        using var store = new SqliteChangeRequestStore(_database.Options);
        var ex = Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));

        Assert.Equal(StorageFailureCategory.UnsupportedLayout, ex.Category);
        Assert.Equal(before, Hash(_database.FilePath));
    }

    [Fact]
    public void AVersionNinetyNineFile_IsStillRefused()
    {
        Run(VersionOneTables + "PRAGMA user_version = 99;");
        var before = Hash(_database.FilePath);

        using var store = new SqliteChangeRequestStore(_database.Options);
        var ex = Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));

        Assert.Equal(StorageFailureCategory.UnsupportedLayout, ex.Category);
        Assert.Equal(before, Hash(_database.FilePath));
    }
}
