using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>
/// SC-006: no storage failure carries a path, folder name or driver text. The stack trace in
/// ToString() legitimately names this library's own types, so ToString() is checked for the
/// location and the driver namespace only.
/// </summary>
public sealed class NoLeakTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private List<(StorageUnavailableException Exception, string Path)> Failures()
    {
        var failures = new List<(StorageUnavailableException, string)>();

        // Directory cannot be created.
        var blocking = Path.Combine(_database.Folder, "secret-blocking-name");
        File.WriteAllText(blocking, "x");
        var blocked = Path.Combine(blocking, "secret-inner-name", "changeops.db");
        using (var store = new SqliteChangeRequestStore(new SqliteStorageOptions(blocked)))
            failures.Add((Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid())), blocked));

        // The path is a directory.
        using (var store = new SqliteChangeRequestStore(new SqliteStorageOptions(_database.Folder)))
            failures.Add((Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid())), _database.Folder));

        // Unsupported layout.
        var stamped = Path.Combine(_database.Folder, "secret-stamped.db");
        using (var connection = new SqliteConnection($"Data Source={stamped};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE t (a INTEGER); PRAGMA user_version = 42;";
            command.ExecuteNonQuery();
        }

        using (var store = new SqliteChangeRequestStore(new SqliteStorageOptions(stamped)))
            failures.Add((Assert.Throws<StorageUnavailableException>(() =>
                store.SaveAuditEntry(SampleRecords.Entry(Guid.NewGuid()))), stamped));

        // Not a database at all.
        var text = Path.Combine(_database.Folder, "secret-text.db");
        File.WriteAllText(text, "synthetic text, not a database");
        using (var store = new SqliteChangeRequestStore(new SqliteStorageOptions(text)))
            failures.Add((Assert.Throws<StorageUnavailableException>(() =>
                store.GetAuditEntries(Guid.NewGuid())), text));

        return failures;
    }

    [Fact]
    public void EveryFailure_HasAFixedMessageAndNoInnerException()
    {
        foreach (var (exception, _) in Failures())
        {
            Assert.Equal("The change request storage is unavailable.", exception.Message);
            Assert.Null(exception.InnerException);
            Assert.Empty(exception.Data);
        }
    }

    [Fact]
    public void EveryFailure_NamesNoPathFolderOrDriver()
    {
        foreach (var (exception, path) in Failures())
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
            var all = exception.Message + "\n" + exception;

            Assert.DoesNotContain(path, all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("changeops-tests-", all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-", all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Microsoft.Data", all, StringComparison.Ordinal);
            if (folder.Length > 0)
                Assert.DoesNotContain(folder, all, StringComparison.OrdinalIgnoreCase);

            foreach (var forbidden in new[] { "sqlite", "SQL", "Microsoft.Data" })
                Assert.DoesNotContain(forbidden, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ARefusedDuplicateVersion_AlsoCarriesOnlyTheFixedMessage()
    {
        using var store = new SqliteChangeRequestStore(_database.Options);
        var id = Guid.NewGuid();
        store.SaveAgentOutput(SampleRecords.Output(id, 1), SampleRecords.Entry(id, minute: 1));

        var ex = Assert.Throws<StorageVersionConflictException>(() =>
            store.SaveAgentOutput(SampleRecords.Output(id, 1), SampleRecords.Entry(id, minute: 2)));

        Assert.Equal("The request conflicts with a concurrent change; try again.", ex.Message);
        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(_database.FilePath, ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
