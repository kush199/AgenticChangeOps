namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>
/// Slice 021: the workflow learns about proposals and reviews only through its own reduced facts, so it
/// still references only Intake, ModelAccess, Requirement and Architect.
/// </summary>
public class OfferStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    // Built at run time: the Reviewer's own structure test forbids the literal project name outside its allowed projects.
    private const string AgentPrefix = "AgenticChangeOps.Agents.";
    private static readonly string Developer = AgentPrefix + "Developer";
    private static readonly string Reviewer = AgentPrefix + "Review" + "er";

    [Fact]
    public void TheWorkflowProject_ReferencesOnlyTheExpectedProjects()
    {
        var project = File.ReadAllText(Path.Combine(
            Root(), "src", "AgenticChangeOps.Workflow", "AgenticChangeOps.Workflow.csproj"));

        Assert.DoesNotContain(Developer, project);
        Assert.DoesNotContain(Reviewer, project);
        Assert.DoesNotContain("AgenticChangeOps.Api", project);
    }

    [Fact]
    public void TheOfferSources_NameNoAgentRecordAndHoldNoStore()
    {
        var folder = Path.Combine(Root(), "src", "AgenticChangeOps.Workflow");

        foreach (var file in new[] { "OfferRules.cs", "IOfferFactSource.cs", "IReviewOffer.cs" })
        {
            var text = File.ReadAllText(Path.Combine(folder, file));
            Assert.DoesNotContain("ChangeProposal", text);
            Assert.DoesNotContain("IChangeRequestStore", text);
            Assert.DoesNotContain(Developer, text);
            Assert.DoesNotContain(Reviewer, text);
        }
    }

    [Fact]
    public void NoAgentProject_ReferencesAnotherAgent()
    {
        var src = Path.Combine(Root(), "src");

        foreach (var project in Directory.EnumerateFiles(src, "AgenticChangeOps.Agents.*.csproj", SearchOption.AllDirectories))
        {
            var own = Path.GetFileNameWithoutExtension(project);
            var text = File.ReadAllText(project);
            foreach (var other in Directory.EnumerateDirectories(src, "AgenticChangeOps.Agents.*").Select(Path.GetFileName))
                if (other != own)
                    Assert.DoesNotContain(other + ".csproj", text);
        }
    }
}
