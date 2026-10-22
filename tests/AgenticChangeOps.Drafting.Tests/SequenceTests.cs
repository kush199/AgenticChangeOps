using AgenticChangeOps.Drafting.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>The order of the steps, proven once for every agent.</summary>
public sealed class SequenceTests
{
    private readonly EngineTestFixture _f = new();

    [Fact]
    public async Task TheStepsRunInOrder_LookupEligibilityGenerateReview_ThenTheLockedStep()
    {
        var id = _f.SeedRequest();

        var outcome = await _f.RunAsync(id);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(
            new[] { "eligibility", "generate", "review", "eligibility", "current", "save" },
            _f.Agent.Calls);
    }

    [Fact]
    public async Task AnUnknownId_ReturnsTheAgentsErrorWithNoCategory_AndTheGeneratorIsNeverCalled()
    {
        var unknown = Guid.NewGuid();

        var outcome = await _f.RunAsync(unknown);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.FailureCategory);
        Assert.Null(outcome.Rejection);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal("ChangeRequestId", error.FieldName);
        Assert.Equal($"No change request with id {unknown} exists.", error.Message);
        Assert.Equal(0, _f.Agent.GenerateCalls);
        Assert.Equal(new[] { "eligibility" }, _f.Agent.Calls);
    }

    [Fact]
    public async Task AnIneligibleRequest_ReturnsTheAgentsErrorWithNoCategory_StoresNothing_AndTheGeneratorIsNeverCalled()
    {
        var id = _f.SeedRequest(ChangeRequestStatus.Submitted);

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.FailureCategory);
        Assert.Equal(StandInAgent.NotEligibleField, Assert.Single(outcome.Errors).FieldName);
        Assert.Equal(0, _f.Agent.GenerateCalls);
        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
    }
}
