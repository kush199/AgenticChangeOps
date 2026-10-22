using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Developer.Tests;

/// <summary>
/// Everything specific to the Developer agent is declared in its own project, that project
/// references only intake and model access, and it names no other agent and not the sample-code
/// project (the file lookup it needs is an interface implemented in the composition root).
/// </summary>
public sealed class DeveloperStructureTests
{
    private const string AgentProject = "AgenticChangeOps.Agents.Developer";

    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static readonly string[] AgentTypes =
    {
        "ChangeProposal", "ProposedChange", "ProposalStatus", "ProposalChangeKind", "ProposalRejection",
        "ProposalContent", "ProposedChangeContent", "ValidatedProposal", "ProposalValidation", "ProposalLimits",
        "ProposalRequirementsBasis", "ProposalDesignBasis", "ProposalBasis", "IProposalBasisSource",
        "ISampleFiles", "SamplePathState", "SampleTextResult",
        "IProposalGenerator", "AzureOpenAIProposalGenerator", "ProposalPromptBuilder", "ProposalResponseParser",
        "ProposalContentValidator", "ProposalVerifier", "ProposalResult", "IProposalService", "ProposalService"
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

    public static IEnumerable<object[]> TypeNames() => AgentTypes.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void AgentType_IsDeclaredOnce_AndOnlyInTheAgentProject(string typeName)
    {
        var src = Path.Combine(RepositoryRoot(), "src");
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
    public void AgentProject_Source_NamesNoOtherAgent_AndNotTheSampleCodeProject()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var files = SourceFiles(folder).ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"AgenticChangeOps\.Agents\.(?!Developer\b)\w+|AgenticChangeOps\.SampleCode|ModelContextProtocol"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentProject_Source_NamesNoOtherAgentsTypes()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                @"(?<!"")\bDesignDraft\b(?!"")|\bDesignDraftingService\b|IDesignDraftGenerator|\bRequirementsDraftingService\b|IRequirementsDraftGenerator|\bRequirementsBasis\b"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentProject_Source_UsesNoFrameworkType()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"using\s+Microsoft\.(Agents|Extensions\.AI)|using\s+ModelContextProtocol"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentProject_Source_NeverWritesRunsOrDeletes()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                @"System\.Diagnostics\.Process|\bProcess\.Start\b|File\.(Write|Create|Delete|Move|Copy|Append|Open)|Directory\.(Create|Delete|Move)|FileStream|StreamWriter|HttpClient"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Intake_NamesNoProposalOrDeveloperType()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.Intake");
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bChangeProposal\b|AgenticChangeOps\.Agents\.Developer"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
