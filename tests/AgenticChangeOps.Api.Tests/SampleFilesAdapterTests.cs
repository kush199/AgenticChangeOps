using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Composition;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// The adapter that lets the Developer's verification read the sample. Every path goes through the
/// sample's own confinement rules; nothing is ever opened for writing; a refusal never carries the
/// supplied path.
/// </summary>
public sealed class SampleFilesAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "changeops-adapter-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside;

    public SampleFilesAdapterTests()
    {
        _outside = Path.Combine(Path.GetTempPath(), "changeops-adapter-outside-" + Guid.NewGuid().ToString("N"));
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

    private SampleFilesAdapter Adapter(int maxFileBytes = 400) => new(_root, maxFileBytes);

    [Fact]
    public void AFile_AFolder_AndAMissingPath_AreTold_Apart()
    {
        var adapter = Adapter();

        Assert.Equal(SamplePathState.File, adapter.Probe("svc/config/a.settings"));
        Assert.Equal(SamplePathState.Folder, adapter.Probe("svc/config"));
        Assert.Equal(SamplePathState.Missing, adapter.Probe("svc/config/missing"));
        Assert.Equal(SamplePathState.Missing, adapter.Probe("svc/new/folder/file.md"));
    }

    [Fact]
    public void AFile_IsReadAsTextExactly()
    {
        var result = Adapter().ReadText("svc/config/a.settings");

        Assert.Null(result.Rejection);
        // The sample's own reader hands out LF text, which is also what the model quotes from.
        Assert.Equal("page.size = 25\nretries = 2\n", result.Text);
    }

    [Fact]
    public void AMissingFile_IsFileNotFound_AndAFolder_IsNot_AFile()
    {
        Assert.Equal(ProposalRejection.FileNotFound, Adapter().ReadText("svc/config/missing").Rejection);
        Assert.Equal(ProposalRejection.FileNotFound, Adapter().ReadText("svc/config").Rejection);
    }

    [Fact]
    public void AFileOverThePerFileLimit_AndABinaryFile_CannotBeVerified()
    {
        Assert.Equal(ProposalRejection.FileNotFound, Adapter(maxFileBytes: 400).ReadText("svc/big").Rejection);
        Assert.Equal(ProposalRejection.FileNotFound, Adapter().ReadText("svc/binary").Rejection);
    }

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

        var state = adapter.Probe(path);
        var read = adapter.ReadText(path);

        Assert.Equal(SamplePathState.OutsideSample, state);
        Assert.Equal(ProposalRejection.PathOutsideSample, read.Rejection);
        Assert.Null(read.Text);
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
            // textual spellings above and by the SampleCode project's own link tests.
            return;
        }

        Assert.Equal(SamplePathState.OutsideSample, Adapter().Probe("svc/link"));
        Assert.Equal(SamplePathState.OutsideSample, Adapter().Probe("svc/link/secret.txt"));
        var read = Adapter().ReadText("svc/link/secret.txt");
        Assert.Equal(ProposalRejection.PathOutsideSample, read.Rejection);
        Assert.Null(read.Text);
    }

    [Fact]
    public void AMissingSampleFolder_ReportsEveryPathMissing_AndNeverCreatesIt()
    {
        var missing = Path.Combine(Path.GetTempPath(), "changeops-adapter-missing-" + Guid.NewGuid().ToString("N"));
        var adapter = new SampleFilesAdapter(missing, 400);

        Assert.NotEqual(SamplePathState.File, adapter.Probe("svc/config/a.settings"));
        Assert.NotNull(adapter.ReadText("svc/config/a.settings").Rejection);
        Assert.False(Directory.Exists(missing));
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

        adapter.Probe("svc/config/a.settings");
        adapter.Probe("svc/new/file.md");
        adapter.ReadText("svc/config/a.settings");
        adapter.ReadText("../secret.txt");

        Assert.Equal(before, Fingerprint());
    }

    [Fact]
    public void TheAdapterSource_NeverOpensAnythingForWriting()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        Assert.True(directory is not null);
        var source = File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "AgenticChangeOps.Api", "Composition", "SampleFilesAdapter.cs"));

        foreach (var forbidden in new[] { "File.Write", "File.Create", "File.Delete", "File.Move", "File.Copy", "File.Append", "FileMode.Create", "FileAccess.Write", "Directory.Create", "Process" })
            Assert.DoesNotContain(forbidden, source);
    }
}
