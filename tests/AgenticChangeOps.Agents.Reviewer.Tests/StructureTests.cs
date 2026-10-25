using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Reviewer.Tests;

/// <summary>
/// Everything specific to the Reviewer agent is declared in its own project, that project references only
/// drafting, intake and model access, and it names no other agent and not the sample-code project (the
/// file lookup it needs is an interface implemented in the composition root). It uses no tool, writes,
/// runs and deletes nothing, and the parts that make decisions cannot see it.
/// </summary>
public sealed class StructureTests
{
    private const string AgentProject = "AgenticChangeOps.Agents.Reviewer";

    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static readonly string[] AgentTypes =
    {
        "Review", "ReviewFinding", "ReviewReference", "SystemCheck", "EditCheck", "ReviewStatus", "SystemCheckStatus",
        "EditCheckOutcome", "FindingCategory", "FindingSeverity", "ReviewDocument", "ReferenceList", "ReviewRejection",
        "ReviewChangeKind", "ReviewLimits", "ReviewContent", "ReviewFindingContent", "ValidatedReview", "ReviewValidation",
        "ReviewRequirementsBasis", "ReviewDesignBasis", "ReviewProposalBasis", "ReviewProposedChange", "ReviewBases",
        "IReviewBasisSource", "IReviewSampleFiles", "ReviewSamplePathState", "SampleChecker", "ReferenceResolver",
        "ReviewContentValidator", "IReviewGenerator", "AzureOpenAIReviewGenerator", "ReviewPromptBuilder",
        "ReviewResponseParser", "ReviewResult", "IReviewService", "ReviewService", "ReviewStoreExtensions"
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
    public void AgentProject_ReferencesOnlyDraftingIntakeAndModelAccess()
    {
        var path = Path.Combine(RepositoryRoot(), "src", AgentProject, AgentProject + ".csproj");

        var references = Regex.Matches(File.ReadAllText(path), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "AgenticChangeOps.Drafting", "AgenticChangeOps.Intake", "AgenticChangeOps.ModelAccess" }, references);
    }

    [Fact]
    public void AgentProject_Source_NamesNoOtherAgent_AndNotTheSampleCodeProject()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var files = SourceFiles(folder).ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"AgenticChangeOps\.Agents\.(?!Reviewer\b)\w+|AgenticChangeOps\.SampleCode|ModelContextProtocol"))
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
                @"(?<!"")\bDesignDraft\b(?!"")|\bDesignDraftingService\b|IDesignDraftGenerator|\bRequirementsDraftingService\b|IRequirementsDraftGenerator|(?<!"")\bRequirementsDraft\b(?!"")|\bChangeProposal\b|\bIProposalService\b|IProposalGenerator|\bProposalVerifier\b"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentProject_Source_UsesNoFrameworkType_AndNoToolType()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                @"using\s+Microsoft\.(Agents|Extensions\.AI)|using\s+ModelContextProtocol|\bIToolSession\b|\bIToolSessionFactory\b|\bIToolChatModelClient\b|\bToolSet\b|\bToolCallRecord\b"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentProject_Source_NeverWritesRunsOrDeletes_AndReadsNothingItself()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                @"System\.Diagnostics\.Process|\bProcess\.Start\b|\bFile\.\w+|\bDirectory\.\w+|FileStream|StreamWriter|StreamReader|HttpClient|System\.IO\b"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("AgenticChangeOps.Workflow")]
    [InlineData("AgenticChangeOps.Intake")]
    [InlineData("AgenticChangeOps.Persistence")]
    [InlineData("AgenticChangeOps.Drafting")]
    [InlineData("AgenticChangeOps.ModelAccess")]
    [InlineData("AgenticChangeOps.SampleCode")]
    public void TheSharedAndDecisionProjects_HaveNoReferenceToTheReviewer(string project)
    {
        var csproj = Path.Combine(RepositoryRoot(), "src", project, project + ".csproj");

        Assert.DoesNotContain(AgentProject, File.ReadAllText(csproj), StringComparison.Ordinal);
    }

    [Fact]
    public void Intake_NamesNoReviewType_ExceptTheInertAuditEventValue()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.Intake");
        var offenders = SourceFiles(folder)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bReviewFinding\b|\bSystemCheck\b|\bIReviewService\b|AgenticChangeOps\.Agents\.Reviewer"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
        Assert.Contains("ReviewGenerated", File.ReadAllText(Path.Combine(folder, "Models", "Enums", "AuditEventType.cs")));
    }
}
