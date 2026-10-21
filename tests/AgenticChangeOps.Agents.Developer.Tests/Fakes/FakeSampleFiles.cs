namespace AgenticChangeOps.Agents.Developer.Tests.Fakes;

/// <summary>
/// In-memory sample: a map of relative path to text. Paths are case-sensitive and any path that
/// starts with "../", "/" or contains ":" or a backslash is outside the sample. Never touches a disk.
/// </summary>
public sealed class FakeSampleFiles : ISampleFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public int ReadCount { get; private set; }

    public FakeSampleFiles With(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    /// <summary>The same small sample the valid fake content edits.</summary>
    public static FakeSampleFiles Standard() => new FakeSampleFiles()
        .With(Fakes.FakeProposalGenerator.SettingsPath, Fakes.FakeProposalGenerator.SettingsText)
        .With("orders-service/config/orders.settings", "page_size = 20\nretry_count = 3\n")
        .With("orders-service/src/OrderIntake", "accept order\nvalidate order\n");

    public SamplePathState Probe(string path)
    {
        if (Escapes(path))
            return SamplePathState.OutsideSample;
        if (_files.ContainsKey(path))
            return SamplePathState.File;
        var prefix = path.TrimEnd('/') + "/";
        return _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal))
            ? SamplePathState.Folder
            : SamplePathState.Missing;
    }

    public SampleTextResult ReadText(string path)
    {
        ReadCount++;
        if (Escapes(path))
            return new SampleTextResult(null, ProposalRejection.PathOutsideSample);
        return _files.TryGetValue(path, out var text)
            ? new SampleTextResult(text, null)
            : new SampleTextResult(null, ProposalRejection.FileNotFound);
    }

    private static bool Escapes(string path) =>
        path.StartsWith("../", StringComparison.Ordinal)
        || path.Contains("/../", StringComparison.Ordinal)
        || path.StartsWith('/')
        || path.Contains(':')
        || path.Contains('\\');
}
