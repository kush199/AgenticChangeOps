using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Live;

/// <summary>
/// Manual, opt-in check against the real model. Not part of the routine suite: it is skipped
/// unless AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1 and needs the model settings and a signed-in
/// identity. It reports only whether the answer parsed into the six required sections; it
/// writes nothing from the prompt, the answer, the endpoint or the deployment name.
/// </summary>
public class LiveDesignCheckTests
{
    private static readonly ChangeRequest SyntheticRequest = new(
        ChangeRequestId: Guid.NewGuid(),
        Title: "Move nightly report schedule",
        Description: "Move the nightly synthetic report job on the Sample Billing Portal to a quieter " +
                     "window so it no longer overlaps the batch import.",
        Requester: "Avery Quill",
        AffectedSystem: "Sample Billing Portal",
        Priority: Priority.Medium,
        ChangeType: ChangeType.ConfigChange,
        Status: ChangeRequestStatus.Approved,
        SubmittedAt: DateTime.UtcNow);

    [LiveModelFact]
    public async Task RealModel_ReturnsAnAnswerThatParsesIntoTheSixSections()
    {
        var generator = AzureOpenAIDesignGenerator.FromProcessEnvironment();

        var content = await generator.GenerateAsync(
            SyntheticRequest, FakeRequirementsBasisSource.Basis(), CancellationToken.None);

        Assert.True(content is not null, "The generator returned no content.");
        Assert.True(content!.ApproachSummary is not null, "The answer had no approach summary section.");
        Assert.True(content.AffectedComponents is not null, "The answer had no affected components section.");
        Assert.True(content.ImpactAndRisks is not null, "The answer had no impact and risks section.");
        Assert.True(content.Dependencies is not null, "The answer had no dependencies section.");
        Assert.True(content.RollbackConsiderations is not null, "The answer had no rollback considerations section.");
        Assert.True(content.OpenQuestions is not null, "The answer had no open questions section.");
    }
}
