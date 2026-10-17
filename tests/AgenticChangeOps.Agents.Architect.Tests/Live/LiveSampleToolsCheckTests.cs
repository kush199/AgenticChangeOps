using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect.Tests.Live;

/// <summary>
/// Manual, opt-in check against the real model with the sample codebase tools. Skipped unless
/// AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1; needs the model settings, a signed-in identity and the tools
/// switched on (AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED=true). It reports only whether files were read
/// and that they exist; it writes nothing from the prompt, the answer, the endpoint or the
/// deployment name. Not run as part of the routine suite.
/// </summary>
public class LiveSampleToolsCheckTests
{
    private static readonly ChangeRequest SyntheticRequest = new(
        ChangeRequestId: Guid.NewGuid(),
        Title: "Raise the order page size",
        Description: "Raise the number of orders shown per page in the orders service.",
        Requester: "Avery Quill",
        AffectedSystem: "Orders Service",
        Priority: Priority.Medium,
        ChangeType: ChangeType.ConfigChange,
        Status: ChangeRequestStatus.Approved,
        SubmittedAt: DateTime.UtcNow);

    private static string SampleFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return Path.Combine(directory!.FullName, "sample-codebase");
    }

    [LiveModelFact]
    public async Task RealModel_WithTools_ListsOnlyFilesThatExist()
    {
        var options = AzureOpenAIGeneratorOptions.FromEnvironment(Environment.GetEnvironmentVariable);
        var settings = SampleCode.SampleToolSettings.FromConfiguration(Environment.GetEnvironmentVariable);
        Assert.True(settings.Enabled, "Set AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED=true for this check.");

        var generator = new AzureOpenAIDesignGenerator(
            options,
            new AgentFrameworkChatModelClient(options),
            new SampleCode.SampleToolSessionFactory(settings, SampleFolder()),
            new AgentFrameworkToolChatModelClient(options));

        var content = await generator.GenerateAsync(
            SyntheticRequest, FakeRequirementsBasisSource.Basis(), CancellationToken.None);

        Assert.True(content?.SampleFilesRead is not null, "The generator returned no file list.");
        Assert.All(content!.SampleFilesRead!, file =>
            Assert.True(File.Exists(Path.Combine(SampleFolder(), file.Replace('/', Path.DirectorySeparatorChar))),
                "A listed file does not exist in the sample."));
    }
}
