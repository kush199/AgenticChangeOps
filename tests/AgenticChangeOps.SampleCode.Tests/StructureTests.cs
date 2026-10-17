using System.Text.RegularExpressions;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>
/// Dependency and read-only rules for the SampleCode project (plan.md structure decision, FR-006):
/// who may reference it, what it may reference, and which APIs it may never call.
/// </summary>
public sealed class StructureTests
{
    private const string Project = "AgenticChangeOps.SampleCode";

    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static IEnumerable<string> Files(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar).Any(p => SkippedDirectories.Contains(p)));

    private static IEnumerable<string> ReferencedProjects(string csproj) =>
        Regex.Matches(File.ReadAllText(csproj), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')));

    [Fact]
    public void TheProject_ReferencesOnlyModelAccess()
    {
        var csproj = Path.Combine(RepositoryRoot(), "src", Project, Project + ".csproj");

        Assert.Equal(new[] { "AgenticChangeOps.ModelAccess" }, ReferencedProjects(csproj));
    }

    [Fact]
    public void OnlyTheApiProject_AndTheTestProjects_ReferenceIt()
    {
        var root = RepositoryRoot();
        var referencing = Files(root, "*.csproj")
            .Where(f => ReferencedProjects(f).Contains(Project))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var allowed = new[]
        {
            "AgenticChangeOps.Agents.Architect.Tests", "AgenticChangeOps.Api", "AgenticChangeOps.Api.Tests",
            "AgenticChangeOps.SampleCode.Tests"
        };
        Assert.Empty(referencing.Except(allowed));
        Assert.Contains("AgenticChangeOps.Api", referencing);
    }

    [Fact]
    public void TheSource_NeverWrites_Runs_OrReachesANetwork()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", Project);
        var banned = new[]
        {
            "File.Write", "File.Create", "File.Delete", "File.Move", "File.Copy", "File.Append", "File.Replace",
            "FileMode.Create", "FileMode.CreateNew", "FileMode.Append", "FileMode.Truncate", "FileMode.OpenOrCreate",
            "FileAccess.Write", "FileAccess.ReadWrite", "Directory.Create", "Directory.Delete", "Directory.Move",
            "Process.Start", "ProcessStartInfo", "HttpClient", "Socket", "TcpClient", "TcpListener", "WebRequest",
            "UdpClient", "Assembly.Load", "Activator.CreateInstance", "Environment.GetEnvironmentVariable"
        };

        var files = Files(folder, "*.cs").ToList();
        Assert.NotEmpty(files);
        var offenders = files
            .SelectMany(f => banned.Where(b => File.ReadAllText(f).Contains(b, StringComparison.Ordinal))
                .Select(b => $"{Path.GetFileName(f)}: {b}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoSourceInTheProject_NamesAFrameworkNamespace()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", Project);
        var framework = new Regex(@"\bMicrosoft\.(Agents|Extensions\.AI)\b");

        Assert.Empty(Files(folder, "*.cs").Where(f => framework.IsMatch(File.ReadAllText(f))));
    }

    [Fact]
    public void NoOtherProduct_Project_NamesTheMcpNamespaces()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var mcp = new Regex(@"\bModelContextProtocol\b");

        var users = Files(src, "*.cs")
            .Where(f => mcp.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(src, f).Split(Path.DirectorySeparatorChar)[0])
            .Distinct()
            .ToArray();

        Assert.Equal(new[] { Project }, users);
    }

    [Fact]
    public void TheArchitectIntakeAndWorkflowSources_NameNeitherMcpNorFrameworkTypes()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var banned = new Regex(@"\bModelContextProtocol\b|\bMicrosoft\.(Agents|Extensions\.AI)\b");

        foreach (var project in new[] { "AgenticChangeOps.Agents.Architect", "AgenticChangeOps.Intake", "AgenticChangeOps.Workflow" })
            Assert.Empty(Files(Path.Combine(src, project), "*.cs").Where(f => banned.IsMatch(File.ReadAllText(f))));
    }
}
