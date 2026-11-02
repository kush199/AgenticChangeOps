using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignDecisionConcurrencyTests
{
    private const int Callers = 16;

    [Fact]
    public async Task OverlappingDecisions_OnOneVersion_StoreExactlyOne_AndRefuseTheRest()
    {
        var f = new DesigningTestFixture();
        var id = await f.ApproveAndDesignAsync();
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, Callers).Select(i => Task.Run(() =>
        {
            gate.Wait();
            return f.Deciding.Decide(
                id, 1, "Approver " + i, i % 2 == 0 ? DesignVerdict.Accept : DesignVerdict.Reject, "Because.");
        })).ToArray();
        gate.Set();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(Callers - 1, results.Count(r => r.Refusal == DesignDecisionRefusal.AlreadyDecided));
        Assert.Single(f.Deciding.GetDecisions(id));
        Assert.Equal(1, f.AuditCount(id, AuditEventType.DesignDecided));
    }

    [Fact]
    public async Task DecisionsOnDifferentRequests_DoNotInterfere()
    {
        var f = new DesigningTestFixture();
        var ids = new List<Guid>();
        for (var i = 0; i < 8; i++)
            ids.Add(await f.ApproveAndDesignAsync());
        using var gate = new ManualResetEventSlim(false);

        var tasks = ids.Select(id => Task.Run(() =>
        {
            gate.Wait();
            return f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);
        })).ToArray();
        gate.Set();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(ids, id => Assert.Single(f.Deciding.GetDecisions(id)));
    }

    [Fact]
    public async Task ADecision_RacingANewDesign_IsEitherOnTheThenLatestVersion_OrRefusedAsOutdated()
    {
        for (var round = 0; round < 20; round++)
        {
            var f = new DesigningTestFixture();
            var id = await f.ApproveAndDesignAsync();
            using var gate = new ManualResetEventSlim(false);

            var decide = Task.Run(() =>
            {
                gate.Wait();
                return f.Deciding.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);
            });
            var design = Task.Run(async () =>
            {
                gate.Wait();
                return await f.Designing.DesignAsync(id);
            });
            gate.Set();
            var result = await decide.WaitAsync(TimeSpan.FromSeconds(30));
            await design.WaitAsync(TimeSpan.FromSeconds(30));

            if (result.IsSuccess)
                Assert.Equal(new[] { 1 }, f.Deciding.GetDecisions(id).Select(d => d.Version));
            else
            {
                Assert.Equal(DesignDecisionRefusal.Outdated, result.Refusal);
                Assert.Empty(f.Deciding.GetDecisions(id));
            }
        }
    }
}
