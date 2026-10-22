using AgenticChangeOps.Drafting.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>What the engine does with the agent's decision about generated content.</summary>
public sealed class ReviewTests
{
    private readonly EngineTestFixture _f = new();

    private void AssertNoLockedStepAndNothingStored(Guid id)
    {
        Assert.DoesNotContain("current", _f.Agent.Calls);
        Assert.DoesNotContain("save", _f.Agent.Calls);
        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
    }

    [Fact]
    public async Task UnusableContent_ReturnsTheAgentsErrors_WithMalformedOutput()
    {
        var id = _f.SeedRequest();
        _f.Agent.Generate = _ => Task.FromResult<StandInGenerated?>(new StandInGenerated("invalid"));

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, outcome.FailureCategory);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal("Text", error.FieldName);
        Assert.Equal("The text is unusable.", error.Message);
        AssertNoLockedStepAndNothingStored(id);
    }

    [Fact]
    public async Task ARejection_IsReturnedAsTheAgentsReason_WithNoRecordNoErrorsAndNoCategory()
    {
        var id = _f.SeedRequest();
        _f.Agent.Generate = _ => Task.FromResult<StandInGenerated?>(new StandInGenerated("forbidden"));

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(StandInRejection.Forbidden, outcome.Rejection);
        Assert.Null(outcome.Record);
        Assert.Empty(outcome.Errors);
        Assert.Null(outcome.FailureCategory);
        AssertNoLockedStepAndNothingStored(id);
    }

    [Fact]
    public async Task ANullGeneration_IsPassedToTheAgentsReviewUnchanged()
    {
        var id = _f.SeedRequest();
        _f.Agent.Generate = _ => Task.FromResult<StandInGenerated?>(null);

        var outcome = await _f.RunAsync(id);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, outcome.FailureCategory);
        Assert.Contains("review", _f.Agent.Calls);
        AssertNoLockedStepAndNothingStored(id);
    }
}
