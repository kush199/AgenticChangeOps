using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignConcurrencyTests
{
    private const int Requests = 10;

    [Fact]
    public async Task OverlappingRequests_TakeDistinctConsecutiveVersionsWithOneAuditEntryEach()
    {
        var f = new DesigningTestFixture();
        var id = f.ApproveWithBasis();
        var gate = new TaskCompletionSource();
        f.Generator.Behavior = async (_, _, _) =>
        {
            await gate.Task;
            return FakeDesignGenerator.ValidContent();
        };

        var pending = Enumerable.Range(0, Requests).Select(_ => f.Designing.DesignAsync(id)).ToArray();
        Assert.True(SpinWait.SpinUntil(() => f.Generator.CallCount == Requests, TimeSpan.FromSeconds(10)));
        gate.SetResult();
        var results = await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        var expected = Enumerable.Range(1, Requests).ToArray();
        Assert.Equal(expected, results.Select(r => r.Draft!.Version).OrderBy(v => v));
        Assert.Equal(expected, f.Store.GetDesigns(id).Select(d => d.Version));
        Assert.Equal(Requests, f.AuditCount(id, AuditEventType.DesignGenerated));
    }

    [Fact]
    public async Task ParallelDesignsAndReads_KeepEveryRequestConsistent()
    {
        var f = new DesigningTestFixture();
        var ids = Enumerable.Range(0, 20).Select(_ => f.ApproveWithBasis()).ToArray();
        using var gate = new ManualResetEventSlim(false);

        var designers = ids.Select(id => Task.Run(async () =>
        {
            gate.Wait();
            return await f.Designing.DesignAsync(id);
        })).ToArray();

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            gate.Wait();
            for (var i = 0; i < 500; i++)
            {
                var id = ids[i % ids.Length];
                Assert.True(f.Designing.GetDesignHistory(id).IsSuccess);
                Assert.True(f.Designing.GetLatestDesign(id).IsSuccess);
            }
        })).ToArray();

        gate.Set();
        await Task.WhenAll(designers).WaitAsync(TimeSpan.FromSeconds(60));
        await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(60));

        foreach (var id in ids)
        {
            var designs = f.Designing.GetDesignHistory(id).Value!;
            Assert.Single(designs);
            Assert.Equal(designs.Count, f.AuditCount(id, AuditEventType.DesignGenerated));
        }
    }
}
