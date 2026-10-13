using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public class GeneratorConfigurationTests
{
    private static AzureOpenAIGeneratorOptions Load(FakeEnvironment env) =>
        AzureOpenAIGeneratorOptions.FromEnvironment(env.Get);

    // --- the routine suite never reaches a real model ------------------------------------

    [Fact]
    public void OutsideTheLiveFolder_NoTestCreatesTheRealClientOrReadsTheProcessEnvironment()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        Assert.True(directory is not null, "Could not locate the repository root.");

        var testsRoot = Path.Combine(directory!.FullName, "tests");
        var liveFolder = Path.Combine(testsRoot, "AgenticChangeOps.Agents.Requirement.Tests", "Live") + Path.DirectorySeparatorChar;
        var thisFile = Path.GetFullPath(Path.Combine(
            testsRoot, "AgenticChangeOps.Agents.Requirement.Tests", "Unit", "GeneratorConfigurationTests.cs"));
        // Assembled from parts so this guard does not match itself.
        var forbidden = new[] { "AzureOpenAI" + "ChatModelClient", "Create" + "FromEnvironment" };

        var offenders = Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(Path.GetFullPath)
            .Where(f => !f.StartsWith(liveFolder, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(f, thisFile, StringComparison.OrdinalIgnoreCase))
            .Where(f => forbidden.Any(word => File.ReadAllText(f).Contains(word, StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void GeneratorCreation_RejectsMissingOptionsOrClient()
    {
        var options = Load(FakeEnvironment.Valid());

        Assert.Throws<ArgumentNullException>(() => new AzureOpenAIDraftGenerator(null!, new FakeChatModelClient()));
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAIDraftGenerator(options, null!));
    }
}
