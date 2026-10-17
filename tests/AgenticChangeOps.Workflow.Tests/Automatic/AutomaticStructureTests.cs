using System.Text.RegularExpressions;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

/// <summary>Guards for the automatic-advancement code in the Workflow project (plan.md, tasks T027).</summary>
public class AutomaticStructureTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticChangeOps.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string[] WorkflowSources()
    {
        var folder = Path.Combine(Root, "src", "AgenticChangeOps.Workflow");
        return Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();
    }

    private static string[] AutomaticSources() =>
        new[]
        {
            "AutomaticAdvancementService.cs", "AutomaticAdvancementSettings.cs", "AutomaticAdvancementStatus.cs",
            "AutomaticStatusSource.cs", "CheckSummary.cs", "CheckSummaryHolder.cs",
            "IAutomaticStepRunner.cs", "IAutomaticStatusSource.cs"
        }.Select(name => Path.Combine(Root, "src", "AgenticChangeOps.Workflow", name)).ToArray();

    [Fact]
    public void TheAutomaticFilesExist()
    {
        Assert.All(AutomaticSources(), path => Assert.True(File.Exists(path), path));
    }

    [Theory]
    [InlineData("AzureOpenAI")]
    [InlineData("IChatModelClient")]
    [InlineData("IRequirementsDraftGenerator")]
    [InlineData("IDesignDraftGenerator")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions.Logging")]
    [InlineData("ILogger")]
    public void TheAutomaticFilesNameNoModelTransportOrLoggingType(string forbidden)
    {
        foreach (var file in AutomaticSources())
            Assert.DoesNotContain(forbidden, File.ReadAllText(file));
    }

    [Theory]
    [InlineData("new ChangeRequestAuditEntry(")]
    [InlineData("new RequirementsDraft(")]
    [InlineData("new DesignDraft(")]
    [InlineData("RecordDecision")]
    [InlineData("SaveAgentOutput")]
    [InlineData("SaveAuditEntry")]
    [InlineData("SaveWithAudit")]
    [InlineData("UpdateWithAudit")]
    public void WorkflowNeverBuildsRecordsOrRecordsDecisions(string forbidden)
    {
        foreach (var file in WorkflowSources())
            Assert.DoesNotContain(forbidden, File.ReadAllText(file));
    }

    [Fact]
    public void TheOnlyStoreWritesInWorkflow_AreTheAttemptBookkeepingMembers()
    {
        var writes = new Regex(@"_store\.(Save|Update|Clear)\w*\(");
        var found = WorkflowSources()
            .SelectMany(file => writes.Matches(File.ReadAllText(file)).Select(m => m.Value))
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.All(found, call => Assert.Matches(@"_store\.(SaveAutomaticAttempt|ClearAutomaticAttempt)\(", call));
    }

    [Fact]
    public void TheRulesFile_KnowsNothingAboutAutomaticAdvancement()
    {
        var text = File.ReadAllText(Path.Combine(Root, "src", "AgenticChangeOps.Workflow", "WorkflowRules.cs"));

        Assert.DoesNotContain("Automatic", text);
        Assert.DoesNotContain("Settings", text);
        Assert.DoesNotContain("Attempt", text);
    }
}
