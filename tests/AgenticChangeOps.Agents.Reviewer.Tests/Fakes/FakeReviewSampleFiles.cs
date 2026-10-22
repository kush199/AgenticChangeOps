namespace AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

/// <summary>
/// In-memory sample: a map of relative path to text. Paths are case-sensitive and any path that
/// starts with "../", "/" or contains ":" or a backslash is outside the sample. Never touches a
/// disk, and the interface it implements has no way to write.
/// </summary>
public sealed class FakeReviewSampleFiles : IReviewSampleFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    public bool IsAvailable { get; set; } = true;

    public int ProbeCount { get; private set; }

    public int ReadCount { get; private set; }

    public FakeReviewSampleFiles With(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    /// <summary>A file that exists but cannot be read.</summary>
    public FakeReviewSampleFiles WithUnreadable(string path)
    {
        _unreadable.Add(path);
        return this;
    }

    public ReviewSamplePathState Probe(string path)
    {
        ProbeCount++;
        if (Escapes(path))
            return ReviewSamplePathState.OutsideSample;
        if (_files.ContainsKey(path) || _unreadable.Contains(path))
            return ReviewSamplePathState.File;
        var prefix = path.TrimEnd('/') + "/";
        return _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal))
            ? ReviewSamplePathState.Folder
            : ReviewSamplePathState.Missing;
    }

    public string? ReadText(string path)
    {
        ReadCount++;
        if (Escapes(path) || _unreadable.Contains(path))
            return null;
        return _files.TryGetValue(path, out var text) ? text : null;
    }

    private static bool Escapes(string path) =>
        path.StartsWith("../", StringComparison.Ordinal)
        || path.Contains("/../", StringComparison.Ordinal)
        || path.StartsWith('/')
        || path.Contains(':')
        || path.Contains('\\');
}
