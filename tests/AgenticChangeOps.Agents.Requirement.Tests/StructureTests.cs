using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Requirement.Tests;

/// <summary>
/// Dependency rules D5 and D6 of slice 007 (contracts/dependency-rules.md): everything specific
/// to the Requirement agent is declared in its own project, and that project references only
/// intake and model access (never another agent).
/// </summary>
public sealed class StructureTests
{
    private const string AgentProject = "AgenticChangeOps.Agents.Requirement";

    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static readonly string[] AgentTypes =
    {
        "RequirementsDraft", "DraftContent", "DraftingResult", "DraftStatus", "DraftContentValidator",
        "IRequirementsDraftGenerator", "IRequirementsDraftingService", "RequirementsDraftingService",
        "AzureOpenAIDraftGenerator", "DraftPromptBuilder", "DraftResponseParser"
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static IEnumerable<string> AllSourceFiles(string root)
    {
        var src = Path.Combine(root, "src");
        Assert.True(Directory.Exists(src), $"Expected folder {src} to exist.");

        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(src, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)));
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void AgentType_IsDeclaredOnce_AndOnlyInTheAgentProject(string typeName)
    {
        var root = RepositoryRoot();
        var declaration = new Regex($@"\b(class|record|interface|enum|struct)\s+{Regex.Escape(typeName)}\b");
        var agentFolder = Path.Combine(root, "src", AgentProject) + Path.DirectorySeparatorChar;

        var declaring = AllSourceFiles(root)
            .Where(f => declaration.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFullPath)
            .ToList();

        var only = Assert.Single(declaring);
        Assert.StartsWith(agentFolder, only, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentProject_ReferencesOnlyIntakeModelAccessAndDrafting()
    {
        var path = Path.Combine(RepositoryRoot(), "src", AgentProject, AgentProject + ".csproj");
        Assert.True(File.Exists(path), $"Expected {path} to exist.");

        var references = Regex.Matches(File.ReadAllText(path), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "AgenticChangeOps.Drafting", "AgenticChangeOps.Intake", "AgenticChangeOps.ModelAccess" }, references);
    }

    [Fact]
    public void AgentProject_Source_NamesNoOtherAgent()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");

        var offenders = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"AgenticChangeOps\.Agents\.(?!Requirement\b)\w+"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    public static IEnumerable<object[]> TypeNames() => AgentTypes.Select(t => new object[] { t });
}
