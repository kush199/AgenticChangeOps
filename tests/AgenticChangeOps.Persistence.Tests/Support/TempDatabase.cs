using IoDirectory = System.IO.Directory;
using IoPath = System.IO.Path;

namespace AgenticChangeOps.Persistence.Tests.Support;

/// <summary>A unique database path under the temp folder, with its files removed on dispose.</summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Folder = IoPath.Combine(IoPath.GetTempPath(), "changeops-tests-" + Guid.NewGuid().ToString("N"));
        IoDirectory.CreateDirectory(Folder);
        FilePath = IoPath.Combine(Folder, "changeops.db");
    }

    public string Folder { get; }

    public string FilePath { get; }

    public SqliteStorageOptions Options => new(FilePath);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            IoDirectory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
