using System.Text.RegularExpressions;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Repository hygiene for the API project (Constitution Principle I, spec FR-010/FR-018). The
/// repository-wide scans in the intake test project already cover src, tests and specs; these
/// add checks specific to the API's own settings and configuration files.
/// </summary>
public class NoSecretsTests
{
    private static string ApiProjectFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return Path.Combine(directory!.FullName, "src", "AgenticChangeOps.Api");
    }

    private static IEnumerable<string> ApiFiles(params string[] extensions) =>
        Directory.EnumerateFiles(ApiProjectFolder(), "*", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
            .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void ApiSource_DefinesNoSettingNameThatCouldHoldASecret()
    {
        var upperSnakeLiteral = new Regex("\"([A-Z][A-Z0-9_]{4,})\"");
        var secretWords = new[] { "KEY", "TOKEN", "SECRET", "PASSWORD" };

        var offenders = new List<string>();
        foreach (var file in ApiFiles(".cs"))
        {
            foreach (Match match in upperSnakeLiteral.Matches(File.ReadAllText(file)))
            {
                var name = match.Groups[1].Value;
                if (secretWords.Any(word => name.Contains(word, StringComparison.Ordinal)))
                    offenders.Add($"{Path.GetFileName(file)}: {name}");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void ApiConfigurationFiles_HoldNoEndpointDeploymentOrCredentialValues()
    {
        var forbidden = new Regex(
            @"openai\.azure\.com|deployment|endpoint|api[-_ ]?key|secret|password|token",
            RegexOptions.IgnoreCase);

        var offenders = ApiFiles(".json")
            .Where(f => forbidden.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void LaunchProfiles_DoNotSelectTheDevelopmentEnvironment()
    {
        // Development makes the framework add its developer exception page, which the API must
        // never enable (research decision 9).
        var offenders = Directory
            .EnumerateFiles(ApiProjectFolder(), "launchSettings.json", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                "\"ASPNETCORE_ENVIRONMENT\"\\s*:\\s*\"Development\"",
                RegexOptions.IgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ApiConfiguration_BindsOnlyToLoopbackAddresses()
    {
        var addresses = ApiFiles(".json")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"https?://[^""\s;,]+").Select(m => m.Value))
            .ToList();

        Assert.All(addresses, address =>
            Assert.Matches(@"^https?://(127\.0\.0\.1|localhost|\[::1\])(:\d+)?", address));
    }
}
