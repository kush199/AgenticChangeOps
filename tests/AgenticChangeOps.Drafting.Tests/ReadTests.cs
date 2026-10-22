// Blocking is deliberate: the reads are synchronous, and the test proves they wait for the store lock.
#pragma warning disable xUnit1031
using AgenticChangeOps.Drafting.Tests.Fakes;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>The latest and history reads, proven once for every agent.</summary>
public sealed class ReadTests
{
    private readonly EngineTestFixture _f = new();

    [Fact]
    public void AnUnknownId_IsNotFoundForBothReads()
    {
        var unknown = Guid.NewGuid();

        var latest = _f.Engine.GetLatest(unknown);
        var history = _f.Engine.GetHistory(unknown);

        foreach (var errors in new[] { latest.Errors, history.Errors })
        {
            var error = Assert.Single(errors);
            Assert.Equal("ChangeRequestId", error.FieldName);
            Assert.Equal($"No change request with id {unknown} exists.", error.Message);
        }

        Assert.False(latest.IsSuccess);
        Assert.False(history.IsSuccess);
    }

    [Fact]
    public void WithNothingStored_LatestIsASuccessfulNull_AndHistoryIsEmpty()
    {
        var id = _f.SeedRequest();

        var latest = _f.Engine.GetLatest(id);
        var history = _f.Engine.GetHistory(id);

        Assert.True(latest.IsSuccess);
        Assert.Null(latest.Value);
        Assert.True(history.IsSuccess);
        Assert.Empty(history.Value!);
    }

    [Fact]
    public async Task Latest_IsTheLastStored_AndHistoryIsEveryVersionInOrder()
    {
        var id = _f.SeedRequest();
        for (var i = 0; i < 3; i++)
            await _f.RunAsync(id);

        Assert.Equal(3, _f.Engine.GetLatest(id).Value!.Version);
        Assert.Equal(new[] { 1, 2, 3 }, _f.Engine.GetHistory(id).Value!.Select(r => r.Version));
    }

    [Fact]
    public void ABlockedLock_BlocksTheReadsUntilItIsReleased()
    {
        var id = _f.SeedRequest();
        var release = new ManualResetEventSlim(false);
        var holding = new ManualResetEventSlim(false);
        var holder = Task.Run(() =>
        {
            lock (_f.Store.SyncRoot)
            {
                holding.Set();
                release.Wait();
            }
        });
        holding.Wait();

        var latest = Task.Run(() => _f.Engine.GetLatest(id));
        var history = Task.Run(() => _f.Engine.GetHistory(id));

        Assert.False(Task.WaitAll(new Task[] { latest, history }, 300));
        release.Set();
        Assert.True(Task.WaitAll(new Task[] { latest, history, holder }, 5000));
        Assert.True(latest.Result.IsSuccess);
        Assert.True(history.Result.IsSuccess);
    }
}
