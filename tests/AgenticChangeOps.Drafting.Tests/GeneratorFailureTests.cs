using AgenticChangeOps.Drafting.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>Generator failures carry a category or an exception type name and nothing else.</summary>
public sealed class GeneratorFailureTests
{
    private const string Sentinel = "sentinel-model-text https://sentinel-endpoint.invalid/";

    private readonly EngineTestFixture _f = new();

    public static IEnumerable<object[]> Categories() =>
        Enum.GetValues<GenerationFailureCategory>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Categories))]
    public async Task ACategoryFailure_BecomesTheGeneratorErrorNamingTheCategoryOnly(GenerationFailureCategory category)
    {
        var id = _f.SeedRequest();
        _f.Agent.Generate = _ => throw new DraftGenerationException(category);

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(category, outcome.FailureCategory);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal($"The content generator failed ({category}).", error.Message);
        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
    }

    [Fact]
    public async Task AnyOtherException_BecomesTheGeneratorErrorNamingTheTypeOnly_AndNothingItCarriesAppears()
    {
        var id = _f.SeedRequest();
        _f.Agent.Generate = _ => throw new InvalidOperationException(Sentinel, new Exception(Sentinel));

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.FailureCategory);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal("The content generator failed (InvalidOperationException).", error.Message);
        Assert.DoesNotContain("sentinel", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
    }

    [Fact]
    public async Task WhenTheAgentPropagatesCancellation_ACancelledCallersTokenRethrows_AndNothingIsStored()
    {
        var id = _f.SeedRequest();
        _f.Agent.PropagatesCancellation = true;
        _f.Agent.Generate = ct => throw new OperationCanceledException(ct);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _f.RunAsync(id, cts.Token));

        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
    }

    [Fact]
    public async Task WhenTheAgentDoesNotPropagateCancellation_ACancelledCallersTokenIsReportedByTypeName()
    {
        var id = _f.SeedRequest();
        _f.Agent.PropagatesCancellation = false;
        _f.Agent.Generate = ct => throw new OperationCanceledException(ct);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await _f.RunAsync(id, cts.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.FailureCategory);
        Assert.Equal(
            "The content generator failed (OperationCanceledException).",
            Assert.Single(outcome.Errors).Message);
    }

    [Fact]
    public async Task WhenTheAgentPropagatesCancellation_ACancellationWhileTheCallersTokenIsLive_IsReportedByTypeName()
    {
        var id = _f.SeedRequest();
        _f.Agent.PropagatesCancellation = true;
        _f.Agent.Generate = _ => throw new OperationCanceledException();

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            "The content generator failed (OperationCanceledException).",
            Assert.Single(outcome.Errors).Message);
    }
}
