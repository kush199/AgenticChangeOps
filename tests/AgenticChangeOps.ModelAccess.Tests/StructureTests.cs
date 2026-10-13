namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Dependency rules D3 and D4 of slice 007 (contracts/dependency-rules.md): the agent-neutral
/// model-access code names no agent and depends on neither intake nor an agent.
/// </summary>
public sealed class StructureTests
{
    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static string ModelAccessFolder() =>
        Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.ModelAccess");

    private static List<string> SourceFiles()
    {
        var folder = ModelAccessFolder();
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");

        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)))
            .ToList();

        Assert.NotEmpty(files);
        return files;
    }

    [Fact]
    public void ModelAccess_ProjectFile_HasNoProjectReference()
    {
        var path = Path.Combine(ModelAccessFolder(), "AgenticChangeOps.ModelAccess.csproj");
        Assert.True(File.Exists(path), $"Expected {path} to exist.");

        Assert.DoesNotContain("ProjectReference", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("Requirement")]
    [InlineData("RequirementsDraft")]
    [InlineData("DraftPromptBuilder")]
    [InlineData("AgenticChangeOps.Intake")]
    [InlineData("AgenticChangeOps.Agents")]
    public void ModelAccess_Source_NamesNoAgentAndNoIntake(string forbidden)
    {
        var offenders = SourceFiles()
            .Where(f => File.ReadAllText(f).Contains(forbidden, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
