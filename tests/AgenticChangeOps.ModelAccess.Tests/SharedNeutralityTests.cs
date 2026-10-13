using System.Text.RegularExpressions;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Rules N1 to N3 of slice 009 (contracts/dependency-rules.md): the shared code added for
/// agents names no agent, no intake or approval capability and no stored record, so a further
/// agent can use it without editing it. The older scan in <see cref="StructureTests"/> covers
/// the Requirement agent and intake namespaces; these tokens extend it to the Architect agent
/// and to the intake types the shared normaliser could otherwise be tempted to return.
/// </summary>
public sealed class SharedNeutralityTests
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

    private static List<string> SourceFiles()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "AgenticChangeOps.ModelAccess");
        Assert.True(Directory.Exists(folder), $"Expected folder {folder} to exist.");

        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)))
            .ToList();

        Assert.NotEmpty(files);
        return files;
    }

    [Theory]
    [InlineData("Architect")]
    [InlineData("DesignDraft")]
    [InlineData("DesignContent")]
    [InlineData("DesignPromptBuilder")]
    [InlineData("ChangeRequest")]
    [InlineData("ValidationError")]
    [InlineData("ApprovalDecision")]
    [InlineData("AuditEvent")]
    public void SharedSource_NamesNoAgentIntakeOrStoredRecord(string forbidden)
    {
        var offenders = SourceFiles()
            .Where(f => File.ReadAllText(f).Contains(forbidden, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void SharedSource_StillDeclaresEachSharedBehaviourOnce()
    {
        var text = SourceFiles().Select(File.ReadAllText).ToList();

        foreach (var declaration in new[]
                 {
                     "class ModelCaller", "class ModelAnswerReader", "class ModelAnswer",
                     "class SectionNormaliser", "record SectionOutcome<T>"
                 })
        {
            // Not preceded or followed by an identifier character, so "ModelAnswer" does not
            // also match "ModelAnswerReader".
            var pattern = new Regex(@"(?<!\w)" + Regex.Escape(declaration) + @"(?!\w)");
            Assert.Single(text.Where(t => pattern.IsMatch(t)).ToList());
        }
    }
}
