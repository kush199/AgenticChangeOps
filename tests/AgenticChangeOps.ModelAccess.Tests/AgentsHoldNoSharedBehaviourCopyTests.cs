namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Rule N4 of slice 009 (contracts/dependency-rules.md), stated once for every agent: the
/// behaviours that live in this project (the model call, answer reading, text and list
/// normalising) are not copied into any agent. Each token below is something only the shared
/// implementation should contain. The scan enumerates every <c>src/AgenticChangeOps.Agents.*</c>
/// folder, so a further agent is covered without adding or editing a test.
/// </summary>
public sealed class AgentsHoldNoSharedBehaviourCopyTests
{
    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static readonly string Fence = new('`', 3);

    public static IEnumerable<object[]> SharedTokens() => new[]
    {
        new object[] { "CancellationTokenSource" },
        new object[] { "JsonDocument" },
        new object[] { "GetProperty(" },
        new object[] { "IsNullOrWhiteSpace(" },
        new object[] { Fence }
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static List<string> AgentFolders()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        Assert.True(Directory.Exists(src), $"Expected folder {src} to exist.");

        return Directory.EnumerateDirectories(src, "AgenticChangeOps.Agents.*", SearchOption.TopDirectoryOnly)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void AtLeastTheTwoShippedAgents_AreScanned()
    {
        var names = AgentFolders().Select(Path.GetFileName).ToList();

        Assert.Contains("AgenticChangeOps.Agents.Requirement", names);
        Assert.Contains("AgenticChangeOps.Agents.Architect", names);
    }

    [Theory]
    [MemberData(nameof(SharedTokens))]
    public void NoAgentSource_ContainsACopyOfASharedBehaviour(string token)
    {
        var offenders = new List<string>();

        foreach (var folder in AgentFolders())
        {
            var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                    .Any(part => SkippedDirectories.Contains(part)))
                .ToList();
            Assert.NotEmpty(files);

            offenders.AddRange(files
                .Where(f => File.ReadAllText(f).Contains(token, StringComparison.Ordinal))
                .Select(f => $"{Path.GetFileName(folder)}/{Path.GetFileName(f)}"));
        }

        Assert.Empty(offenders);
    }
}
