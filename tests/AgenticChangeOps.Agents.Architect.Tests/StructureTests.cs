using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Architect.Tests;

/// <summary>
/// Rules A1 to A3 of slice 008 (contracts/dependency-rules.md): everything specific to the
/// Architect agent is declared in its own project, that project references only intake and
/// model access, and it never names another agent.
/// </summary>
public sealed class StructureTests
{
    private const string AgentProject = "AgenticChangeOps.Agents.Architect";

    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static readonly string[] AgentTypes =
    {
        "DesignDraft", "DesignContent", "DesignDraftingResult", "DesignDraftStatus", "DesignContentValidator",
        "IDesignDraftGenerator", "IDesignDraftingService", "DesignDraftingService",
        "AzureOpenAIDesignGenerator", "DesignPromptBuilder", "DesignResponseParser",
        "RequirementsBasis", "IRequirementsBasisSource"
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static IEnumerable<string> SourceFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)));

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void AgentType_IsDeclaredOnce_AndOnlyInTheAgentProject(string typeName)
    {
        var root = RepositoryRoot();
        var src = Path.Combine(root, "src");
        Assert.True(Directory.Exists(src), $"Expected folder {src} to exist.");
        var declaration = new Regex($@"\b(class|record|interface|enum|struct)\s+{Regex.Escape(typeName)}\b");
        var agentFolder = Path.Combine(src, AgentProject) + Path.DirectorySeparatorChar;

        var declaring = SourceFiles(src)
            .Where(f => declaration.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFullPath)
            .ToList();

        var only = Assert.Single(declaring);
        Assert.StartsWith(agentFolder, only, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentProject_ReferencesOnlyIntakeAndModelAccess()
    {
        var path = Path.Combine(RepositoryRoot(), "src", AgentProject, AgentProject + ".csproj");
        Assert.True(File.Exists(path), $"Expected {path} to exist.");

        var references = Regex.Matches(File.ReadAllText(path), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "AgenticChangeOps.Intake", "AgenticChangeOps.ModelAccess" }, references);
    }

    [Fact]
    public void AgentProject_Source_NamesNoOtherAgent()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");
        var files = SourceFiles(folder).ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"AgenticChangeOps\.Agents\.(?!Architect\b)\w+"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentProject_Source_NamesNoRequirementAgentType()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"(?<!"")\bRequirementsDraft\b(?!"")|\bRequirementsDraftingService\b|IRequirementsDraftGenerator"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Intake_NamesNoDesignOrArchitectType()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.Intake");
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");
        var files = SourceFiles(folder).ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f), @"\bDesignDraft\b|ArchitectAgent|AgenticChangeOps\.Agents\.Architect"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    public static IEnumerable<object[]> TypeNames() => AgentTypes.Select(t => new object[] { t });
}
