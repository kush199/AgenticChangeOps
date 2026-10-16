namespace AgenticChangeOps.Persistence;

public sealed class SqliteStorageOptions
{
    public SqliteStorageOptions(string path)
    {
        Path = path;
    }

    public string Path { get; }

    /// <summary>
    /// A per-user location outside any repository: local application data, or the user profile
    /// folder when that is empty. Never the working directory.
    /// </summary>
    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(root))
            root = System.IO.Path.GetTempPath();

        return System.IO.Path.Combine(root, "AgenticChangeOps", "changeops.db");
    }
}
