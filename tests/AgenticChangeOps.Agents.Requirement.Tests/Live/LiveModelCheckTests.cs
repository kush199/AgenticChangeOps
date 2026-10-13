using AgenticChangeOps.Intake.AzureOpenAI;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Tests.Live;

/// <summary>
/// Manual, opt-in check against the real model. Not part of the routine suite: it is skipped
/// unless AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1 and needs the model settings and a signed-in
/// identity. It reports only whether the answer parsed into the five required sections; it
/// writes nothing from the prompt, the answer, the endpoint or the deployment name.
/// </summary>
public class LiveModelCheckTests
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
        Status: ChangeRequestStatus.Submitted,
        SubmittedAt: DateTime.UtcNow);

    [LiveModelFact]
    public async Task RealModel_ReturnsAnAnswerThatParsesIntoTheFiveSections()
    {
        var generator = AzureOpenAIDraftGenerator.CreateFromEnvironment();

        var content = await generator.GenerateAsync(SyntheticRequest, CancellationToken.None);

        Assert.True(content is not null, "The generator returned no content.");
        Assert.True(content!.Summary is not null, "The answer had no summary section.");
        Assert.True(content.FunctionalRequirements is not null, "The answer had no functional requirements section.");
        Assert.True(content.AcceptanceCriteria is not null, "The answer had no acceptance criteria section.");
        Assert.True(content.Assumptions is not null, "The answer had no assumptions section.");
        Assert.True(content.OpenQuestions is not null, "The answer had no open questions section.");
    }
}
