using System.Text.RegularExpressions;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>
/// FR-001 to FR-003, SC-007, Principle I: the real sample is exactly what the spec lists, small
/// enough to review in one sitting, entirely invented, and not part of the build.
/// </summary>
public sealed class SampleHygieneTests
{
    private static readonly string[] ExpectedFiles =
    {
        "README.md",
        "billing-portal/config/portal.settings",
        "billing-portal/notes/decisions.md",
        "billing-portal/notes/design.md",
        "billing-portal/src/InvoiceList",
        "billing-portal/src/PaymentReminder",
        "billing-portal/src/StatementExport",
        "orders-service/config/orders.settings",
        "orders-service/notes/decisions.md",
        "orders-service/notes/design.md",
        "orders-service/src/OrderIntake",
        "orders-service/src/OrderStatusTracker",
        "orders-service/src/StockCheck"
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static string SampleRoot() => Path.Combine(RepositoryRoot(), "sample-codebase");

    private static string[] SampleFiles() =>
        Directory.EnumerateFiles(SampleRoot(), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(SampleRoot(), f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void TheSample_ContainsExactlyTheListedFiles()
    {
        Assert.Equal(ExpectedFiles.OrderBy(f => f, StringComparer.Ordinal), SampleFiles());
    }

    [Fact]
    public void EachSystem_HasConfigurationSourceFilesAndDesignNotes()
    {
        foreach (var system in new[] { "orders-service", "billing-portal" })
        {
            Assert.Contains(SampleFiles(), f => f.StartsWith(system + "/config/"));
            Assert.True(SampleFiles().Count(f => f.StartsWith(system + "/src/")) >= 3);
            Assert.Contains(SampleFiles(), f => f.StartsWith(system + "/notes/"));
        }
    }

    [Fact]
    public void EveryFile_IsUnder3000Bytes_AndTheWholeFolderUnder30000()
    {
        var sizes = SampleFiles().Select(f => (File: f, Bytes: new FileInfo(Path.Combine(SampleRoot(), f)).Length)).ToList();

        Assert.All(sizes, s => Assert.True(s.Bytes < 3000, $"{s.File} is {s.Bytes} bytes."));
        Assert.True(sizes.Sum(s => s.Bytes) < 30000);
    }

    [Fact]
    public void EveryFile_IsPlainText_WithNoCompilableExtension()
    {
        foreach (var file in SampleFiles())
        {
            var bytes = File.ReadAllBytes(Path.Combine(SampleRoot(), file));
            Assert.DoesNotContain((byte)0, bytes);
            Assert.DoesNotContain(new[] { ".cs", ".csproj", ".sln", ".props", ".targets", ".vb", ".fs" },
                ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void NoFile_ContainsAnEmail_ARealHost_OrAKeyLikeString()
    {
        var email = new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");
        var url = new Regex(@"https?://([^/\s""']+)", RegexOptions.IgnoreCase);
        var keyLike = new Regex(@"(?i)(api[_-]?key|secret|token|password|passwd|connectionstring|BEGIN [A-Z ]*PRIVATE KEY|sk-[A-Za-z0-9]{8,}|AKIA[0-9A-Z]{8,})");
        var longBlob = new Regex(@"[A-Za-z0-9]{32,}");
        var allowedHosts = new[] { "example.com", "example.org", "example.net" };

        foreach (var file in SampleFiles())
        {
            var text = File.ReadAllText(Path.Combine(SampleRoot(), file));
            Assert.False(email.IsMatch(text), $"{file} contains an email-like string.");
            Assert.False(keyLike.IsMatch(text), $"{file} contains a key or secret word.");
            Assert.False(longBlob.IsMatch(text), $"{file} contains a long token-like string.");
            foreach (Match match in url.Matches(text))
                Assert.Contains(allowedHosts, h => match.Groups[1].Value.Equals(h, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void TheSample_IsNotPartOfTheSolution_AndAddsNoCompileItemToAnyProject()
    {
        var root = RepositoryRoot();
        Assert.DoesNotContain("sample-codebase", File.ReadAllText(Path.Combine(root, "AgenticChangeOps.sln")));

        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));
        foreach (var project in projects)
        {
            var text = File.ReadAllText(project);
            foreach (Match match in Regex.Matches(text, @"<Compile\b[^>]*sample-codebase[^>]*>"))
                Assert.Fail($"{Path.GetFileName(project)} compiles the sample: {match.Value}");
        }
    }

    [Fact]
    public void TheOnlyProjectThatMentionsTheSample_IsTheApiProject_AsCopiedContent()
    {
        var root = RepositoryRoot();
        var mentioning = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
            .Where(f => File.ReadAllText(f).Contains("sample-codebase", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Equal(new[] { "AgenticChangeOps.Api.csproj" }, mentioning);
    }

    [Fact]
    public void BothSystemNames_MatchTheNamesUsedByTheProjectsSyntheticChangeRequests()
    {
        var readme = File.ReadAllText(Path.Combine(SampleRoot(), "README.md"));

        Assert.Contains("orders-service", readme);
        Assert.Contains("billing-portal", readme);
    }
}
