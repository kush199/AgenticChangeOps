using System.Text.RegularExpressions;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>
/// Keeps the shared skeleton agent-neutral and keeps the sequence in one place. The scan over
/// the agents finds their folders by pattern, so a fourth agent is covered with no edit here.
/// </summary>
public sealed class StructureTests
{
    private const string DraftingProject = "AgenticChangeOps.Drafting";

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string DraftingFolder() => Path.Combine(RepositoryRoot(), "src", DraftingProject);

    private static IEnumerable<string> SourceFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Regex.IsMatch(f, @"[\\/](bin|obj)[\\/]"));

    private static IEnumerable<string> ReferencedProjects(string csproj) =>
        Regex.Matches(File.ReadAllText(csproj), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal);

    [Theory]
    [InlineData(@"\b(Requirement|Requirements|Architect|Developer|Design|Proposal|Proposals)\w*")]
    [InlineData(@"\b(RequirementsDraft|DesignDraft|ChangeProposal|DraftContent|DesignContent|ProposalContent)\b")]
    [InlineData(@"\b(ApprovalDecision|ApprovalService|ClearanceStatus|AutomaticAttempt|AutomaticAdvancement)\w*")]
    [InlineData(@"AgenticChangeOps\.(Agents|Workflow|Api|Persistence|SampleCode)\b")]
    public void DraftingSource_NamesNoAgentNoStoredRecordNoApprovalAndNoAutomaticAdvancement(string pattern)
    {
        var files = SourceFiles(DraftingFolder()).ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(f => Regex.IsMatch(StripComments(File.ReadAllText(f)), pattern))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void DraftingProject_ReferencesOnlyIntakeAndModelAccess_AndNoPackage()
    {
        var csproj = Path.Combine(DraftingFolder(), DraftingProject + ".csproj");

        Assert.Equal(new[] { "AgenticChangeOps.Intake", "AgenticChangeOps.ModelAccess" }, ReferencedProjects(csproj));
        Assert.DoesNotContain("PackageReference", File.ReadAllText(csproj));
    }

    [Fact]
    public void ModelAccess_StillHasNoProjectReference()
    {
        var csproj = Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.ModelAccess", "AgenticChangeOps.ModelAccess.csproj");

        Assert.DoesNotContain("ProjectReference", File.ReadAllText(csproj));
    }

    [Fact]
    public void NoAgentProject_ReferencesAnotherAgent_AndEachReferencesDrafting()
    {
        var agentProjects = Directory.GetDirectories(Path.Combine(RepositoryRoot(), "src"), "AgenticChangeOps.Agents.*");
        Assert.NotEmpty(agentProjects);

        foreach (var folder in agentProjects)
        {
            var name = Path.GetFileName(folder);
            var references = ReferencedProjects(Path.Combine(folder, name + ".csproj")).ToList();

            Assert.DoesNotContain(references, r => r.StartsWith("AgenticChangeOps.Agents.", StringComparison.Ordinal));
            Assert.Contains(DraftingProject, references);
        }
    }

    [Fact]
    public void NoAgentSource_HoldsACopyOfTheSequence()
    {
        // The generator-failure translation and the next-version rule exist once, in Drafting.
        var banned = new[] { "The content generator failed (", "catch (DraftGenerationException", "Count + 1" };
        var offenders = new List<string>();

        foreach (var folder in Directory.GetDirectories(Path.Combine(RepositoryRoot(), "src"), "AgenticChangeOps.Agents.*"))
            foreach (var file in SourceFiles(folder))
            {
                var text = StripComments(File.ReadAllText(file));
                offenders.AddRange(banned.Where(text.Contains).Select(b => $"{Path.GetFileName(file)}: {b}"));
            }

        Assert.Empty(offenders);
    }

    private static string StripComments(string source) =>
        Regex.Replace(source, @"//.*|/\*[\s\S]*?\*/", string.Empty);
}
