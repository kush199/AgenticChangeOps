using System.Security.Cryptography;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>D3 / FR-009 / FR-010: the layout version is recorded, and unknown layouts are refused untouched.</summary>
public sealed class LayoutVersionTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private int UserVersion()
    {
        using var connection = new SqliteConnection($"Data Source={_database.FilePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void CreateSqliteFile(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={_database.FilePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void AssertRefusedUntouched()
    {
        var before = Hash(_database.FilePath);
        var files = Directory.GetFiles(_database.Folder).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        using var store = new SqliteChangeRequestStore(_database.Options);

        var ex = Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));
        Assert.Equal(StorageFailureCategory.UnsupportedLayout, ex.Category);
        var ex2 = Assert.Throws<StorageUnavailableException>(() =>
            SampleRecords.WriteOne(store));
        Assert.Equal(StorageFailureCategory.UnsupportedLayout, ex2.Category);

        Assert.Equal(before, Hash(_database.FilePath));
        Assert.Equal(files, Directory.GetFiles(_database.Folder).Select(Path.GetFileName).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void ANewFile_IsStampedWithCurrentLayoutVersion()
    {
        using (var store = new SqliteChangeRequestStore(_database.Options))
            Assert.Null(store.FindById(Guid.NewGuid()));

        Assert.Equal(2, UserVersion());
    }

    [Fact]
    public void AFileStampedWithAnotherVersion_IsRefused_AndLeftUnchanged()
    {
        CreateSqliteFile("CREATE TABLE change_requests (id TEXT PRIMARY KEY, payload TEXT); PRAGMA user_version = 99;");

        AssertRefusedUntouched();
    }

    [Fact]
    public void AForeignSqliteFileWithOtherTables_IsRefused_AndLeftUnchanged()
    {
        CreateSqliteFile("CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO widgets VALUES (1, 'synthetic');");

        AssertRefusedUntouched();
    }

    [Fact]
    public void ANonSqliteFile_IsRefused_AndLeftUnchanged()
    {
        File.WriteAllText(_database.FilePath, "this is not a database, it is synthetic text");

        AssertRefusedUntouched();
    }

    [Fact]
    public void AnExistingVersionOneFile_IsUsedAsIs()
    {
        var request = SampleRecords.Request();
        using (var store = new SqliteChangeRequestStore(_database.Options))
            store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));

        using var reopened = new SqliteChangeRequestStore(_database.Options);

        RecordAssert.Same(request, reopened.FindById(request.ChangeRequestId));
        Assert.Equal(2, UserVersion());
    }
}
