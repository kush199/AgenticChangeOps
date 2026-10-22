using AgenticChangeOps.Drafting.Tests.Fakes;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>
/// A fourth agent adds only itself: its own types and rules (<see cref="StandInAgent"/>), a service
/// of a few lines and a save step. This test project references Drafting, Intake and ModelAccess
/// and nothing else, and no shared or agent file is edited for it.
/// </summary>
public sealed class StandInFourthAgentTests
{
    private sealed class StandInService
    {
        private readonly StandInAgent _agent;
        private readonly IIdGenerator _ids;
        private readonly DraftingEngine<StandInRecord, StandInBasis, StandInGenerated, StandInContent, StandInRejection> _engine;

        public StandInService(EngineTestFixture f)
        {
            _agent = f.Agent;
            _ids = f.Ids;
            _engine = f.Engine;
        }

        public Task<DraftingOutcome<StandInRecord, StandInRejection>> ProposeAsync(Guid id) =>
            _engine.RunAsync(id, CancellationToken.None, (current, basis, content, version, at) =>
                _agent.Save(_ids, current, basis, content, version, at));

        public QueryResult<StandInRecord?> Latest(Guid id) => _engine.GetLatest(id);

        public QueryResult<IReadOnlyList<StandInRecord>> History(Guid id) => _engine.GetHistory(id);
    }

    [Fact]
    public async Task AFourthAgent_ProducesStoresAndReadsItsOwnOutputsThroughTheSkeleton()
    {
        var f = new EngineTestFixture();
        var service = new StandInService(f);
        var id = f.SeedRequest();

        var first = await service.ProposeAsync(id);
        var second = await service.ProposeAsync(id);

        Assert.Equal(1, first.Record!.Version);
        Assert.Equal(2, second.Record!.Version);
        Assert.Equal(2, service.Latest(id).Value!.Version);
        Assert.Equal(new[] { 1, 2 }, service.History(id).Value!.Select(r => r.Version));
    }

    [Fact]
    public async Task AFourthAgent_KeepsItsOwnEligibilityRules()
    {
        var f = new EngineTestFixture();
        var service = new StandInService(f);
        var id = f.SeedRequest(Intake.Models.Enums.ChangeRequestStatus.Rejected);

        var outcome = await service.ProposeAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(StandInAgent.NotEligibleField, Assert.Single(outcome.Errors).FieldName);
    }
}
