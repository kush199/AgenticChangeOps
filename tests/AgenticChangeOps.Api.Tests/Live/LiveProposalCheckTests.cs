using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.SampleCode;

namespace AgenticChangeOps.Api.Tests.Live;

/// <summary>
/// Marks a test that calls the real model. It is skipped unless a person explicitly opts in by
/// setting <see cref="OptInVariable"/> to 1, so the routine suite, CI, and a bare
/// <c>dotnet test</c> never reach a real model.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveModelFactAttribute : FactAttribute
{
    public const string OptInVariable = "AGENTICCHANGEOPS_LIVE_MODEL_CHECK";

    public LiveModelFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
            Skip = $"Live model check not enabled. Set {OptInVariable}=1 (and the model settings) to run it by hand.";
    }
}

/// <summary>
/// Manual, opt-in check against the real model with the sample codebase tools. Skipped unless
/// AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1; needs the model settings, a signed-in identity and the tools
/// switched on (AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED=true). It reports only whether the files read
/// exist and that the proposal verifies against the real sample; it writes nothing from the prompt,
/// the answer, the endpoint or the deployment name. Not run as part of the routine suite.
/// </summary>
public class LiveProposalCheckTests
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

    [LiveModelFact]
    public async Task RealModel_WithTools_ProposesChangesThatVerifyAgainstTheRealSample()
    {
        var options = AzureOpenAIGeneratorOptions.FromEnvironment(Environment.GetEnvironmentVariable);
        var settings = SampleToolSettings.FromConfiguration(Environment.GetEnvironmentVariable);
        Assert.True(settings.Enabled, "Set AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED=true for this check.");
        var sample = SampleToolsHost.RealSampleFolder();

        var generator = new AzureOpenAIProposalGenerator(
            options,
            new AgentFrameworkChatModelClient(options),
            new SampleToolSessionFactory(settings, sample),
            new AgentFrameworkToolChatModelClient(options));
        var basis = FakeProposalBasis.Current();

        var content = await generator.GenerateAsync(SyntheticRequest, basis.Requirements!, basis.Design!, CancellationToken.None);

        Assert.True(content?.SampleFilesRead is not null, "The generator returned no file list.");
        Assert.All(content!.SampleFilesRead!, file =>
            Assert.True(File.Exists(Path.Combine(sample, file.Replace('/', Path.DirectorySeparatorChar))),
                "A listed file does not exist in the sample."));

        var validation = new ProposalContentValidator().Validate(content);
        Assert.Empty(validation.Errors);
        Assert.Null(validation.Rejection);
        var verifier = new ProposalVerifier(new SampleFilesAdapter(sample, settings.MaxFileBytes), ProposalLimits.Defaults);
        Assert.Null(verifier.Verify(validation.Value!.Changes));
    }
}
