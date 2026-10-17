using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.SampleCode.Tests.Fakes;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>
/// FR-007, SC-003: every path is confined to the sample. At least twenty escaping spellings are
/// refused by the three tools, nothing outside is ever disclosed, and a refusal never echoes the
/// supplied path or any real folder.
/// </summary>
public sealed class PathConfinementTests
{
    public static IEnumerable<object[]> EscapingSpellings()
    {
        var spellings = new[]
        {
            "..",
            "../secret.txt",
            "../outside/secret.txt",
            "orders-service/../../outside/secret.txt",
            "orders-service/..\\..\\outside\\secret.txt",
            "a/../../outside/secret.txt",
            "./../outside/secret.txt",
            "....//outside/secret.txt",
            "%2e%2e/outside/secret.txt",
            "/etc/passwd",
            "\\windows\\win.ini",
            "C:\\Windows\\win.ini",
            "C:/Windows/win.ini",
            "c:",
            "\\\\server\\share\\x",
            "//server/share/x",
            "file:///outside/secret.txt",
            "orders-service/%00",
            "orders-service/\0/x",
            "orders-service./notes/design.md",
            "orders-service/notes/design.md ",
            "orders-service/notes/design.md:stream",
            "",
            "   ",
            new string('a', 10000)
        };

        return spellings.Select(s => new object[] { s });
    }

    private static readonly string[] FixedRefusals =
    {
        "That path is outside the sample codebase.",
        "No such file or folder in the sample codebase.",
        "That argument is not valid."
    };

    private static void AssertRefusedWithoutDisclosure(ToolResult result, string supplied, TempSample sample)
    {
        Assert.NotEqual(ToolCallOutcome.Served, result.Outcome);
        Assert.Equal(0, result.Bytes);
        Assert.Contains(result.Text, FixedRefusals);
        Assert.DoesNotContain(TempSample.SecretOutsideText, result.Text);
        Assert.DoesNotContain(sample.Root, result.Text);
        Assert.DoesNotContain(sample.Outside, result.Text);
        if (supplied.Trim().Length > 2)
            Assert.DoesNotContain(supplied, result.Text);
    }

    [Theory]
    [MemberData(nameof(EscapingSpellings))]
    public void Resolver_RefusesEveryEscapingSpelling(string spelling)
    {
        using var sample = new TempSample();
        var resolver = new SamplePathResolver(sample.Root);

        var resolution = resolver.Resolve(spelling);

        Assert.False(resolution.IsAllowed);
        Assert.Null(resolution.FullPath);
        Assert.NotEqual(ToolCallOutcome.Served, resolution.Refusal);
    }

    [Theory]
    [MemberData(nameof(EscapingSpellings))]
    public void AllThreeTools_RefuseEveryEscapingSpelling_WithAFixedMessage(string spelling)
    {
        using var sample = new TempSample();
        var reader = new SampleReader(new SamplePathResolver(sample.Root), maxFileBytes: 8000);
        var before = sample.Fingerprint();

        AssertRefusedWithoutDisclosure(reader.List(spelling), spelling, sample);
        AssertRefusedWithoutDisclosure(reader.Read(spelling), spelling, sample);

        Assert.Equal(before, sample.Fingerprint());
    }

    [Fact]
    public void TheAbsolutePathOfARealFileOutsideTheSample_IsRefused_InBothSeparatorStyles()
    {
        using var sample = new TempSample();
        var reader = new SampleReader(new SamplePathResolver(sample.Root), maxFileBytes: 8000);
        var absolute = Path.Combine(sample.Outside, "secret.txt");

        foreach (var spelling in new[] { absolute, absolute.Replace('\\', '/') })
        {
            AssertRefusedWithoutDisclosure(reader.Read(spelling), spelling, sample);
            AssertRefusedWithoutDisclosure(reader.List(Path.GetDirectoryName(spelling)), spelling, sample);
        }
    }

    [Fact]
    public void ADirectoryLinkInsideTheSample_ThatPointsOutside_IsRefusedByAllThreeTools()
    {
        using var sample = new TempSample();
        Assert.True(sample.TryCreateDirectoryLink("linkout", sample.Outside),
            "This machine could not create a directory link, so the link-escape case cannot run.");
        var reader = new SampleReader(new SamplePathResolver(sample.Root), maxFileBytes: 8000);

        AssertRefusedWithoutDisclosure(reader.Read("linkout/secret.txt"), "linkout/secret.txt", sample);
        AssertRefusedWithoutDisclosure(reader.List("linkout"), "linkout", sample);

        // Searching must neither follow the link nor find the text behind it.
        var search = reader.Search(TempSample.SecretOutsideText);
        Assert.Equal(ToolCallOutcome.Served, search.Outcome);
        Assert.DoesNotContain("secret.txt", search.Text);
        Assert.DoesNotContain("linkout", search.Text);
    }

    [Fact]
    public void APathThatStaysInsideTheSample_IsAllowed()
    {
        using var sample = new TempSample();
        var resolver = new SamplePathResolver(sample.Root);

        var resolution = resolver.Resolve("orders-service/notes/design.md");

        Assert.True(resolution.IsAllowed);
        Assert.Equal("orders-service/notes/design.md", resolution.RelativePath);
        Assert.StartsWith(Path.GetFullPath(sample.Root), resolution.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheRootItself_IsAllowed_AsTheEmptyListingTarget()
    {
        using var sample = new TempSample();
        var reader = new SampleReader(new SamplePathResolver(sample.Root), maxFileBytes: 8000);

        var listing = reader.List(null);

        Assert.Equal(ToolCallOutcome.Served, listing.Outcome);
        Assert.Contains("orders-service/", listing.Text);
        Assert.Contains("billing-portal/", listing.Text);
    }
}
