using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>D4 / FR-011: storage that cannot be opened is refused, specifically, and recovers.</summary>
public sealed class UnavailableStorageTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    /// <summary>A path whose parent is an ordinary file, so its directory can never be created.</summary>
    private (string BlockingFile, SqliteStorageOptions Options) BlockedLocation()
    {
        var blocking = Path.Combine(_database.Folder, "not-a-folder");
        File.WriteAllText(blocking, "synthetic");
        return (blocking, new SqliteStorageOptions(Path.Combine(blocking, "inner", "changeops.db")));
    }

    [Fact]
    public void Constructing_NeverThrows_AndNeverTouchesTheDisk()
    {
        var folder = Path.Combine(_database.Folder, "not-created-yet");
        var options = new SqliteStorageOptions(Path.Combine(folder, "changeops.db"));

        using var store = new SqliteChangeRequestStore(options);

        Assert.False(Directory.Exists(folder));
        Assert.False(File.Exists(options.Path));
    }

    [Fact]
    public void ALocationWhoseDirectoryCannotBeCreated_IsRefusedAsUnavailable()
    {
        var (_, options) = BlockedLocation();
        using var store = new SqliteChangeRequestStore(options);

        var ex = Assert.Throws<StorageUnavailableException>(() =>
            SampleRecords.WriteOne(store));

        Assert.Equal(StorageFailureCategory.Unavailable, ex.Category);
        Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));
    }

    [Fact]
    public void APathThatIsADirectory_IsRefusedAsUnavailable()
    {
        using var store = new SqliteChangeRequestStore(new SqliteStorageOptions(_database.Folder));

        var ex = Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));

        Assert.Equal(StorageFailureCategory.Unavailable, ex.Category);
    }

    [Fact]
    public void WhenTheLocationBecomesValid_TheSameStoreWorksWithoutRestarting()
    {
        var (blocking, options) = BlockedLocation();
        using var store = new SqliteChangeRequestStore(options);
        Assert.Throws<StorageUnavailableException>(() => store.FindById(Guid.NewGuid()));

        File.Delete(blocking);
        var request = SampleRecords.Request();
        store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
    }

    [Fact]
    public void ARefusedWrite_StoresNothing()
    {
        var (blocking, options) = BlockedLocation();
        using var store = new SqliteChangeRequestStore(options);
        var request = SampleRecords.Request();
        Assert.Throws<StorageUnavailableException>(() =>
            store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId)));

        File.Delete(blocking);

        Assert.Null(store.FindById(request.ChangeRequestId));
        Assert.Empty(store.GetAuditEntries(request.ChangeRequestId));
    }

    [Fact]
    public void DefaultPath_IsAbsolute_EndsInTheAppFolder_AndIsNotUnderTheWorkingDirectory()
    {
        var path = SqliteStorageOptions.DefaultPath();

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("changeops.db", Path.GetFileName(path));
        Assert.Equal("AgenticChangeOps", Path.GetFileName(Path.GetDirectoryName(path)));
        var working = Path.GetFullPath(Directory.GetCurrentDirectory())
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.False(
            Path.GetFullPath(path).StartsWith(working, StringComparison.OrdinalIgnoreCase),
            "The default location must not be inside the working directory.");
    }
}
