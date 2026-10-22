using AgenticChangeOps.Drafting.Tests.Fakes;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>Overlapping runs never share a version; an output and its audit entry are stored together or not at all.</summary>
public sealed class OverlapAndAtomicityTests
{
    private readonly EngineTestFixture _f = new();

    [Fact]
    public async Task OverlappingRuns_TakeDistinctConsecutiveVersions_WithOneAuditEntryEach()
    {
        var id = _f.SeedRequest();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => _f.RunAsync(id))));

        Assert.All(outcomes, o => Assert.True(o.IsSuccess));
        Assert.Equal(Enumerable.Range(1, 32), outcomes.Select(o => o.Record!.Version).OrderBy(v => v));
        Assert.Equal(Enumerable.Range(1, 32), _f.Store.GetAgentOutputs<StandInRecord>(id).Select(r => r.Version));
        var audited = _f.Store.GetAuditEntries(id).Select(e => e.DraftVersion!.Value).OrderBy(v => v);
        Assert.Equal(Enumerable.Range(1, 32), audited);
    }

    [Fact]
    public async Task ASaveThatThrows_ReachesTheCaller_AndLeavesNeitherAnOutputNorAnAuditEntry()
    {
        var id = _f.SeedRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _f.Engine.RunAsync(id, CancellationToken.None, (_, _, _, _, _) => throw new InvalidOperationException("boom")));

        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
        Assert.Equal(0, _f.AuditCount(id));
    }

    [Fact]
    public async Task ARunAfterAFailedSave_TakesVersionOneWithNoGap()
    {
        var id = _f.SeedRequest();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _f.Engine.RunAsync(id, CancellationToken.None, (_, _, _, _, _) => throw new InvalidOperationException("boom")));

        var outcome = await _f.RunAsync(id);

        Assert.Equal(1, outcome.Record!.Version);
    }
}
