namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>
/// D5 / FR-016 / FR-008: the storage layer names no agent and depends only on Intake, and the
/// database files are ignored by git. Source scans only.
/// </summary>
public sealed class StructureTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static string PersistenceFolder() =>
        Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.Persistence");

    private static List<string> SourceFiles()
    {
        var folder = PersistenceFolder();
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");
        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"))
            .ToList();
        Assert.NotEmpty(files);
        return files;
    }

    [Theory]
    [InlineData("AgenticChangeOps.Agents")]
    [InlineData("AgenticChangeOps.ModelAccess")]
    [InlineData("Requirement")]
    [InlineData("Architect")]
    [InlineData("DesignDraft")]
    [InlineData("RequirementsDraft")]
    public void Persistence_Source_NamesNoAgentOrModelAccess(string forbidden)
    {
        var offenders = SourceFiles()
            .Where(f => File.ReadAllText(f).Contains(forbidden, StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persistence_ProjectFile_ReferencesOnlyIntake()
    {
        var project = File.ReadAllText(
            Path.Combine(PersistenceFolder(), "AgenticChangeOps.Persistence.csproj"));

        var references = System.Text.RegularExpressions.Regex.Matches(
            project, "<ProjectReference[^>]*Include=\"([^\"]+)\"");
        var single = Assert.Single(references);
        Assert.Contains("AgenticChangeOps.Intake.csproj", single.Groups[1].Value);
    }

    [Fact]
    public void GitIgnore_ListsTheDatabaseFilePatterns()
    {
        var ignore = File.ReadAllLines(Path.Combine(RepositoryRoot(), ".gitignore"))
            .Select(l => l.Trim()).ToList();

        foreach (var pattern in new[] { "*.db", "*.db-journal", "*.sqlite" })
            Assert.Contains(pattern, ignore);
    }
}
