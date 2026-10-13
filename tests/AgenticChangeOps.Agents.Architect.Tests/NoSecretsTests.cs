using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Architect.Tests;

/// <summary>
/// Repository hygiene for the Architect project (Constitution Principle I). The repository-wide
/// scans in the ModelAccess and API test projects already cover src, tests and specs; this adds
/// the per-agent rule the Requirement agent has: no setting name that could hold a secret.
/// </summary>
public class NoSecretsTests
{
    private const string AgentProject = "AgenticChangeOps.Agents.Architect";

    private static string ProjectFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return Path.Combine(directory!.FullName, "src", AgentProject);
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(ProjectFolder(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));

    [Fact]
    public void ArchitectProjectFolder_ExistsAndIsScanned()
    {
        Assert.True(Directory.Exists(ProjectFolder()), $"{ProjectFolder()} does not exist.");
        Assert.NotEmpty(SourceFiles());
    }

    [Fact]
    public void ArchitectProject_DefinesNoSettingNameThatCouldHoldASecret()
    {
        var upperSnakeLiteral = new Regex("\"([A-Z][A-Z0-9_]{4,})\"");
        var secretWords = new[] { "KEY", "TOKEN", "SECRET", "PASSWORD" };
        var files = SourceFiles().ToList();
        Assert.NotEmpty(files);

        var offenders = new List<string>();
        foreach (var file in files)
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
}
