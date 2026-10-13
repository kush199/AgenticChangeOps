namespace AgenticChangeOps.Intake.Tests.Unit;

/// <summary>
/// Dependency rules D1 and D2 of slice 007 (contracts/dependency-rules.md): intake and approval
/// depend on no agent and no model-access code. Source scans only, so this project needs no
/// reference to either.
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

    private static string IntakeFolder() => Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.Intake");

    private static List<string> SourceFiles(string folder)
    {
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");

        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)))
            .ToList();

        Assert.NotEmpty(files);
        return files;
    }

    [Fact]
    public void Intake_ProjectFile_HasNoProjectReference()
    {
        var project = File.ReadAllText(Path.Combine(IntakeFolder(), "AgenticChangeOps.Intake.csproj"));

        Assert.DoesNotContain("ProjectReference", project);
    }

    [Theory]
    [InlineData("RequirementsDraft")]
    [InlineData("RequirementAgent")]
    [InlineData("AgenticChangeOps.Agents")]
    [InlineData("AgenticChangeOps.ModelAccess")]
    public void Intake_Source_NamesNoAgentOrModelAccess(string forbidden)
    {
        var offenders = SourceFiles(IntakeFolder())
            .Where(f => File.ReadAllText(f).Contains(forbidden, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(IntakeFolder(), f))
            .ToList();

        Assert.Empty(offenders);
    }
}
