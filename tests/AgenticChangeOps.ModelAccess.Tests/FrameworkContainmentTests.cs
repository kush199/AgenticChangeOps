using System.Text.RegularExpressions;
using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// contracts/dependency-rules.md D1 to D3: framework types and packages are confined to the shared
/// model-access project, and within it to the one client that uses them.
/// </summary>
public sealed class FrameworkContainmentTests
{
    private const string ClientFile = "AgentFrameworkChatModelClient.cs";
    private static readonly Regex FrameworkNamespaces = new(@"\bMicrosoft\.(Agents|Extensions\.AI)\b", RegexOptions.Compiled);
    private static readonly Regex FrameworkUsings = new(@"^\s*using\s+Microsoft\.(Agents|Extensions\.AI)\b", RegexOptions.Compiled | RegexOptions.Multiline);

    private static IEnumerable<string> Files(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));

    [Fact]
    public void FrameworkNamespaces_AreUsedInExactlyOneSourceFile_TheClient()
    {
        var src = Path.Combine(WireSnapshot.RepositoryRoot(), "src");

        var users = Files(src, "*.cs")
            .Where(f => FrameworkNamespaces.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(src, f))
            .ToList();

        var only = Assert.Single(users);
        Assert.Equal(Path.Combine("AgenticChangeOps.ModelAccess", ClientFile), only);
    }

    [Fact]
    public void FrameworkPackages_AreReferencedOnlyByTheSharedModelAccessProject()
    {
        var root = WireSnapshot.RepositoryRoot();

        var referencing = Files(root, "*.csproj")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"PackageReference\s+Include=""Microsoft\.(Agents|Extensions\.AI)"))
            .Select(f => Path.GetFileName(f))
            .ToList();

        var only = Assert.Single(referencing);
        Assert.Equal("AgenticChangeOps.ModelAccess.csproj", only);
    }

    [Fact]
    public void ApiAndAgentProjects_NeverUseTheFrameworkNamespaces()
    {
        var src = Path.Combine(WireSnapshot.RepositoryRoot(), "src");

        var offenders = Files(src, "*.cs")
            .Where(f => !Path.GetRelativePath(src, f).StartsWith("AgenticChangeOps.ModelAccess", StringComparison.Ordinal))
            .Where(f => FrameworkUsings.IsMatch(File.ReadAllText(f)) || FrameworkNamespaces.IsMatch(File.ReadAllText(f)))
            .ToList();

        Assert.Empty(offenders);
    }
}
