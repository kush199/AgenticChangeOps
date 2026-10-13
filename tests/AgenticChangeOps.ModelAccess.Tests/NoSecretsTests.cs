using System.Text.RegularExpressions;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Repository hygiene guard (Constitution Principle I, spec FR-011/FR-012): no key, token, real
/// endpoint or deployment name may be committed, and the model-backed generator must not define
/// a setting that could hold a secret.
/// </summary>
public class NoSecretsTests
{
    private static readonly string[] ScannedExtensions = { ".cs", ".csproj", ".json", ".md", ".yml" };
    private static readonly string[] SkippedDirectories = { "bin", "obj", ".git", ".vs", "TestResults" };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static IEnumerable<string> Files(string root, params string[] topLevelFolders)
    {
        var thisFile = Path.GetFullPath(
            Path.Combine(root, "tests", "AgenticChangeOps.ModelAccess.Tests", "NoSecretsTests.cs"));

        foreach (var folder in topLevelFolders)
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var relativeParts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar);
                if (relativeParts.Any(part => SkippedDirectories.Contains(part)))
                    continue;
                if (!ScannedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(Path.GetFullPath(file), thisFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return file;
            }
        }
    }

    private static List<string> Find(string root, Regex pattern, params string[] folders)
    {
        var hits = new List<string>();
        foreach (var file in Files(root, folders))
        {
            var text = File.ReadAllText(file);
            if (pattern.IsMatch(text))
                hits.Add(Path.GetRelativePath(root, file));
        }
        return hits;
    }

    [Fact]
    public void NoLongHexadecimalRuns_InSourceTestsOrSpecs()
    {
        var hits = Find(RepositoryRoot(), new Regex(@"\b[0-9a-fA-F]{32}\b"), "src", "tests", "specs");

        Assert.Empty(hits);
    }

    [Fact]
    public void NoRealResourceEndpoints_InSourceTestsOrSpecs()
    {
        // A resource name directly before the service domain; a bare ".<domain>" in prose is fine.
        var hits = Find(RepositoryRoot(),
            new Regex(@"[A-Za-z0-9-]+\.openai\.azure\.com", RegexOptions.IgnoreCase), "src", "tests", "specs");

        Assert.Empty(hits);
    }

    [Fact]
    public void NoBearerTokens_InSourceTestsOrSpecs()
    {
        var hits = Find(RepositoryRoot(), new Regex(@"Bearer\s+[A-Za-z0-9\-_\.=]{20,}"), "src", "tests", "specs");

        Assert.Empty(hits);
    }

    [Fact]
    public void NoKeyNamedSettingsOrHeaders_InSource()
    {
        var hits = Find(RepositoryRoot(),
            new Regex(@"api[-_ ]?key|AZURE_OPENAI_API_KEY", RegexOptions.IgnoreCase), "src");

        Assert.Empty(hits);
    }

    [Fact]
    public void GeneratorProject_DefinesNoSettingNameThatCouldHoldASecret()
    {
        var root = RepositoryRoot();
        var projectFolder = Path.Combine("src", "AgenticChangeOps.ModelAccess");
        var upperSnakeLiteral = new Regex("\"([A-Z][A-Z0-9_]{4,})\"");
        var secretWords = new[] { "KEY", "TOKEN", "SECRET", "PASSWORD" };

        var offenders = new List<string>();
        foreach (var file in Files(root, projectFolder).Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (Match match in upperSnakeLiteral.Matches(File.ReadAllText(file)))
            {
                var name = match.Groups[1].Value;
                if (secretWords.Any(word => name.Contains(word, StringComparison.Ordinal)))
                    offenders.Add($"{Path.GetFileName(file)}: {name}");
            }
        }

        Assert.Empty(offenders);
    }

    // Slice 007: the same rule for the Requirement agent project, which now holds the generator.
    [Fact]
    public void RequirementAgentProject_DefinesNoSettingNameThatCouldHoldASecret()
    {
        var root = RepositoryRoot();
        var projectFolder = Path.Combine("src", "AgenticChangeOps.Agents.Requirement");
        Assert.True(Directory.Exists(Path.Combine(root, projectFolder)), $"{projectFolder} does not exist.");

        var upperSnakeLiteral = new Regex("\"([A-Z][A-Z0-9_]{4,})\"");
        var secretWords = new[] { "KEY", "TOKEN", "SECRET", "PASSWORD" };

        var sourceFiles = Files(root, projectFolder)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(sourceFiles);

        var offenders = new List<string>();
        foreach (var file in sourceFiles)
        {
            foreach (Match match in upperSnakeLiteral.Matches(File.ReadAllText(file)))
            {
                var name = match.Groups[1].Value;
                if (secretWords.Any(word => name.Contains(word, StringComparison.Ordinal)))
                    offenders.Add($"{Path.GetFileName(file)}: {name}");
            }
        }

        Assert.Empty(offenders);
    }

    // Slice 007 guard: the file walker silently skips a missing folder, so a moved project
    // would make the scan above pass without scanning anything.
    [Fact]
    public void GeneratorProjectFolder_ExistsAndIsScanned()
    {
        var root = RepositoryRoot();
        var projectFolder = Path.Combine("src", "AgenticChangeOps.ModelAccess");

        Assert.True(Directory.Exists(Path.Combine(root, projectFolder)), $"{projectFolder} does not exist.");
        Assert.Contains(
            Files(root, projectFolder),
            file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
    }
}
