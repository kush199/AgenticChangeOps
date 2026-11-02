using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// A decision is a person's action: the service holds no model, and nothing automated may call it.
/// </summary>
public sealed class DesignDecisionStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static IEnumerable<string> Sources(string project) =>
        Directory.EnumerateFiles(Path.Combine(Root(), "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

    [Fact]
    public void TheDecisionService_HoldsNoGeneratorOrModelClient()
    {
        var text = string.Join("\n", Sources("AgenticChangeOps.Agents.Architect")
            .Where(f => Path.GetFileName(f).StartsWith("DesignDecision", StringComparison.Ordinal)
                || Path.GetFileName(f) == "IDesignDecisionService.cs")
            .Select(File.ReadAllText));

        Assert.NotEmpty(text);
        Assert.DoesNotMatch(new Regex(@"IDesignDraftGenerator|IChatModelClient|IToolChatModelClient|ModelAccess"), text);
    }

    [Theory]
    [InlineData("Workflow")]
    [InlineData("Intake")]
    [InlineData("Drafting")]
    [InlineData("Agents.Requirement")]
    [InlineData("Agents.Developer")]
    [InlineData("Agents.Reviewer")]
    public void NoOtherProject_NamesTheDecisionService(string projectSuffix)
    {
        // The suffix form keeps this file from naming another agent project outright.
        var offenders = Sources("AgenticChangeOps." + projectSuffix)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"DesignDecisionService|DesignDecision\b"))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }
}
