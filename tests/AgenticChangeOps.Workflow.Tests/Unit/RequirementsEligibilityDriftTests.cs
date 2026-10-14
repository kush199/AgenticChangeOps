using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>
/// Guards dependency rule 9: the workflow offers a requirements step exactly when the
/// Requirement agent would accept a draft, so the two sets of rules cannot silently diverge.
/// </summary>
public class RequirementsEligibilityDriftTests
{
    public static IEnumerable<object[]> EveryStatus() =>
        Enum.GetValues<ChangeRequestStatus>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public async Task RequirementsStepIsOffered_ExactlyWhenTheAgentAccepts(ChangeRequestStatus status)
    {
        var f = new WorkflowTestFixture();
        var id = f.SubmitSynthetic();
        f.SeedStatus(id, status);

        var offered = f.Workflow.GetStatus(id).Value!.NextStep == WorkflowStep.RequirementsDraft;
        var accepted = (await f.Requirements.DraftAsync(id)).IsSuccess;

        Assert.Equal(accepted, offered);
    }
}
