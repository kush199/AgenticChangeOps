using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// The adapter that lets the system's own re-check read the sample. Every path goes through the sample's
/// own confinement rules; nothing is ever opened for writing; nothing it returns can carry the supplied path.
/// </summary>
public sealed class ReviewSampleFilesAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "changeops-review-adapter-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside;

    public ReviewSampleFilesAdapterTests()
    {
        _outside = Path.Combine(Path.GetTempPath(), "changeops-review-adapter-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "svc", "config"));
        Directory.CreateDirectory(_outside);
        File.WriteAllText(Path.Combine(_root, "svc", "config", "a.settings"), "page.size = 25\r\nretries = 2\r\n");
        File.WriteAllText(Path.Combine(_root, "svc", "big"), new string('x', 500));
        File.WriteAllBytes(Path.Combine(_root, "svc", "binary"), new byte[] { 1, 2, 0, 3, 4 });
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "OUTSIDE-SECRET");
    }

    public void Dispose()
    {
        foreach (var directory in new[] { _root, _outside })
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private ReviewSampleFilesAdapter Adapter(int maxFileBytes = 400) => new(_root, maxFileBytes);

    // ---- availability -----------------------------------------------------------------

    [Fact]
    public void AnExistingSampleFolder_IsAvailable()
    {
        Assert.True(Adapter().IsAvailable);
    }

    [Fact]
    public void AMissingSampleFolder_IsNotAvailable_AndIsNeverCreated()
    {
        var missing = Path.Combine(Path.GetTempPath(), "changeops-review-adapter-missing-" + Guid.NewGuid().ToString("N"));
        var adapter = new ReviewSampleFilesAdapter(missing, 400);

        Assert.False(adapter.IsAvailable);
        Assert.NotEqual(ReviewSamplePathState.File, adapter.Probe("svc/config/a.settings"));
        Assert.Null(adapter.ReadText("svc/config/a.settings"));
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void ARealSampleFolder_IsAvailableToo()
    {
        var adapter = new ReviewSampleFilesAdapter(Fakes.ProposalHost.RealSampleFolder(), 8000);

        Assert.True(adapter.IsAvailable);
        Assert.Equal(ReviewSamplePathState.File, adapter.Probe("orders-service/config/orders.settings"));
    }

    // ---- probing and reading ----------------------------------------------------------

    [Fact]
    public void AFile_AFolder_AndAMissingPath_AreTold_Apart()
    {
        var adapter = Adapter();

        Assert.Equal(ReviewSamplePathState.File, adapter.Probe("svc/config/a.settings"));
        Assert.Equal(ReviewSamplePathState.Folder, adapter.Probe("svc/config"));
        Assert.Equal(ReviewSamplePathState.Missing, adapter.Probe("svc/config/missing"));
        Assert.Equal(ReviewSamplePathState.Missing, adapter.Probe("svc/new/folder/file.md"));
    }

    [Fact]
    public void AFile_IsReadAsTextExactly()
    {
        // The sample own reader hands out LF text, which is also what the model quotes from.
        Assert.Equal("page.size = 25\nretries = 2\n", Adapter().ReadText("svc/config/a.settings"));
    }

    [Fact]
    public void AMissingFile_AFolder_ABinaryFile_AndAFileOverTheLimit_ReadAsNull()
    {
        var adapter = Adapter(maxFileBytes: 400);

        Assert.Null(adapter.ReadText("svc/config/missing"));
        Assert.Null(adapter.ReadText("svc/config"));
        Assert.Null(adapter.ReadText("svc/binary"));
        Assert.Null(adapter.ReadText("svc/big"));
    }

    // ---- confinement ------------------------------------------------------------------

    public static IEnumerable<object[]> EscapingSpellings()
    {
        foreach (var path in new[]
                 {
                     "..", "../x", "../secret.txt", "svc/../../secret.txt", "svc/..\\..\\secret.txt", "/etc/passwd",
                     "\\windows\\win.ini", "C:\\x", "C:/x", "c:", "\\\\server\\share\\x", "//server/x", "%2e%2e/x",
                     "....//x", "./../x", "svc/../../x", "svc/%00", "svc/a\0b", "svc/ ", "svc/.. ", "file:///x",
                     "svc/config/../../../x", new string('a', 10000), "", "   ", "svc//..//..//x"
                 })
            yield return new object[] { path };
    }

    [Theory]
    [MemberData(nameof(EscapingSpellings))]
    public void AnEscapingPath_IsOutsideTheSample_ForProbeAndRead_WithoutDisclosingAnything(string path)
    {
        var adapter = Adapter();

        Assert.Equal(ReviewSamplePathState.OutsideSample, adapter.Probe(path));
        Assert.Null(adapter.ReadText(path));
    }

    [Fact]
    public void ALinkLeadingOutside_IsOutsideTheSample()
    {
        var link = Path.Combine(_root, "svc", "link");
        try
        {
            Directory.CreateSymbolicLink(link, _outside);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            // Links cannot be created here (no permission); the confinement itself is covered by the
            // textual spellings above and by the SampleCode project own link tests.
            return;
        }

        Assert.Equal(ReviewSamplePathState.OutsideSample, Adapter().Probe("svc/link"));
        Assert.Equal(ReviewSamplePathState.OutsideSample, Adapter().Probe("svc/link/secret.txt"));
        Assert.Null(Adapter().ReadText("svc/link/secret.txt"));
    }

    [Fact]
    public void Reading_ChangesNothingUnderTheSample()
    {
        string Fingerprint() => string.Join("|", Directory
            .EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => p + (File.Exists(p) ? File.ReadAllText(p) + File.GetLastWriteTimeUtc(p).Ticks : string.Empty)));
        var before = Fingerprint();
        var adapter = Adapter();

        foreach (var path in new[] { "svc/config/a.settings", "svc/config", "svc/missing", "svc/big", "../secret.txt", "svc/binary" })
        {
            adapter.Probe(path);
            adapter.ReadText(path);
        }
        _ = adapter.IsAvailable;

        Assert.Equal(before, Fingerprint());
        Assert.Equal("OUTSIDE-SECRET", File.ReadAllText(Path.Combine(_outside, "secret.txt")));
    }

    [Fact]
    public void TheAdapterType_OffersNoWayToWrite()
    {
        var names = typeof(ReviewSampleFilesAdapter).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => !n.StartsWith("get_", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "Probe", "ReadText" }, names);
    }
}
