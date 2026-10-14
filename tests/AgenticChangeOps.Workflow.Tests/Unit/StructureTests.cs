using System.Text.RegularExpressions;

namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>Dependency and neutrality rules 1 to 8 of contracts/dependency-rules.md.</summary>
public class StructureTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticChangeOps.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string[] ProjectReferences(string csproj) =>
        Regex.Matches(File.ReadAllText(csproj), "ProjectReference Include=\"[^\"]*?([^\\\\\"]+)\\.csproj\"")
            .Select(m => m.Groups[1].Value)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static string[] WorkflowSources()
    {
        var folder = Path.Combine(Root, "src", "AgenticChangeOps.Workflow");
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();
        Assert.NotEmpty(files);
        return files;
    }

    [Fact]
    public void Rule1_WorkflowReferencesOnlyIntakeModelAccessAndBothAgents()
    {
        var refs = ProjectReferences(
            Path.Combine(Root, "src", "AgenticChangeOps.Workflow", "AgenticChangeOps.Workflow.csproj"));

        Assert.Equal(
            new[]
            {
                "AgenticChangeOps.Agents.Architect", "AgenticChangeOps.Agents.Requirement",
                "AgenticChangeOps.Intake", "AgenticChangeOps.ModelAccess"
            },
            refs);
    }

    [Fact]
    public void Rule2and3_OnlyTheApiReferencesWorkflow_AndTheAgentsStayApart()
    {
        var sourceProjects = Directory.GetFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories);
        Assert.True(sourceProjects.Length >= 6);

        foreach (var project in sourceProjects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var refs = ProjectReferences(project);
            if (name != "AgenticChangeOps.Api")
                Assert.DoesNotContain("AgenticChangeOps.Workflow", refs);
        }

        Assert.DoesNotContain("AgenticChangeOps.Agents.Requirement", ProjectReferences(
            Path.Combine(Root, "src", "AgenticChangeOps.Agents.Architect", "AgenticChangeOps.Agents.Architect.csproj")));
        Assert.DoesNotContain("AgenticChangeOps.Agents.Architect", ProjectReferences(
            Path.Combine(Root, "src", "AgenticChangeOps.Agents.Requirement", "AgenticChangeOps.Agents.Requirement.csproj")));
    }

    [Fact]
    public void Rule4_WorkflowIsNotAWebProject()
    {
        var text = File.ReadAllText(
            Path.Combine(Root, "src", "AgenticChangeOps.Workflow", "AgenticChangeOps.Workflow.csproj"));

        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", text);
        Assert.DoesNotContain("FrameworkReference", text);
        Assert.DoesNotContain("AgenticChangeOps.Api", text);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("HttpContext")]
    [InlineData("AzureOpenAI")]
    [InlineData("IChatModelClient")]
    [InlineData("GeneratorOptions")]
    [InlineData("IRequirementsDraftGenerator")]
    [InlineData("IDesignDraftGenerator")]
    public void Rule5_WorkflowSourceNamesNoTransportOrModelDetail(string forbidden)
    {
        foreach (var file in WorkflowSources())
            Assert.DoesNotContain(forbidden, File.ReadAllText(file));
    }

    [Theory]
    [InlineData("new RequirementsDraft(")]
    [InlineData("new DesignDraft(")]
    [InlineData("new ChangeRequestAuditEntry(")]
    [InlineData("SaveAgentOutput")]
    [InlineData("SaveDraft(")]
    [InlineData("SaveDesign(")]
    [InlineData("RecordDecision")]
    [InlineData("IApprovalService")]
    [InlineData("Store.Update")]
    [InlineData("_store.Update")]
    public void Rule6and7_WorkflowNeverBuildsRecordsOrWritesTheStore(string forbidden)
    {
        foreach (var file in WorkflowSources())
            Assert.DoesNotContain(forbidden, File.ReadAllText(file));
    }

    [Theory]
    [InlineData("IWorkflowService")]
    [InlineData("IRequirementsDraftingService")]
    [InlineData("IDesignDraftingService")]
    [InlineData("IChangeRequestStore")]
    [InlineData("Generator")]
    [InlineData("Task<")]
    public void Rule8_TheRulesFileDependsOnNoServiceOrGenerator(string forbidden)
    {
        var text = File.ReadAllText(
            Path.Combine(Root, "src", "AgenticChangeOps.Workflow", "WorkflowRules.cs"));

        Assert.DoesNotContain(forbidden, text);
    }
}
