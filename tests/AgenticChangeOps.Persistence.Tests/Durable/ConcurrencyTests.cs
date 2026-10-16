using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>D6 / SC-004: overlapping requests for one version, exactly one wins, no gaps.</summary>
public sealed class ConcurrencyTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task FiftyOverlappingSavesForOneVersion_OneSucceeds_FortyNineAreRefusedCleanly_AndVersionsHaveNoGaps()
    {
        using var store = new SqliteChangeRequestStore(_database.Options);
        var id = Guid.NewGuid();
        const int callers = 50;
        var start = new ManualResetEventSlim(false);
        var successes = 0;
        var conflicts = 0;
        var others = new List<Exception>();

        var tasks = Enumerable.Range(0, callers).Select(i => Task.Run(() =>
        {
            start.Wait();
            try
            {
                store.SaveAgentOutput(
                    SampleRecords.Output(id, 1) with { Summary = $"caller {i}" },
                    SampleRecords.Entry(id, AuditEventType.DraftGenerated, minute: i));
                Interlocked.Increment(ref successes);
            }
            catch (StorageVersionConflictException)
            {
                Interlocked.Increment(ref conflicts);
            }
            catch (Exception ex)
            {
                lock (others)
                    others.Add(ex);
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        Assert.Empty(others);
        Assert.Equal(1, successes);
        Assert.Equal(callers - 1, conflicts);
        Assert.Single(store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Single(store.GetAuditEntries(id));

        // Continuing the sequence leaves no gap and no duplicate.
        for (var version = 2; version <= 6; version++)
            store.SaveAgentOutput(SampleRecords.Output(id, version), SampleRecords.Entry(id, minute: 100 + version));

        Assert.Equal(Enumerable.Range(1, 6), store.GetAgentOutputs<FakeAgentOutput>(id).Select(o => o.Version));
        Assert.Equal(6, store.GetAuditEntries(id).Count);
    }

    [Fact]
    public async Task OverlappingSavesForDifferentChangeRequestsAndTypes_AllSucceed()
    {
        using var store = new SqliteChangeRequestStore(_database.Options);
        var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        var start = new ManualResetEventSlim(false);
        var failures = new List<Exception>();

        void Run(Action action)
        {
            start.Wait();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                lock (failures)
                    failures.Add(ex);
            }
        }

        var tasks = ids.SelectMany(id => new[]
        {
            Task.Run(() => Run(() => store.SaveAgentOutput(SampleRecords.Output(id, 1), SampleRecords.Entry(id, minute: 1)))),
            Task.Run(() => Run(() => store.SaveAgentOutput(new OtherFakeOutput(id, 1, "n"), SampleRecords.Entry(id, minute: 2))))
        }).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        Assert.Empty(failures);
        foreach (var id in ids)
        {
            Assert.Single(store.GetAgentOutputs<FakeAgentOutput>(id));
            Assert.Single(store.GetAgentOutputs<OtherFakeOutput>(id));
            Assert.Equal(2, store.GetAuditEntries(id).Count);
        }
    }
}
