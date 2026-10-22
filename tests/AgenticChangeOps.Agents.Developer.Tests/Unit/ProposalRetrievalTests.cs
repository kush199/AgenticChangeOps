using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>US2: latest and history, versions preserved, concurrent requests get distinct versions.</summary>
public sealed class ProposalRetrievalTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task Latest_IsTheNewestVersion_AndHistoryIsInAscendingOrder()
    {
        var id = _f.ApproveWithDrafts();
        var first = (await _f.Proposing.ProposeAsync(id)).Proposal!;
        var second = (await _f.Proposing.ProposeAsync(id)).Proposal!;

        Assert.Equal(2, second.Version);
        Assert.Equal(second, _f.Proposing.GetLatestProposal(id).Value);
        var history = _f.Proposing.GetProposalHistory(id).Value!;
        Assert.Equal(new[] { 1, 2 }, history.Select(p => p.Version));
        Assert.Equal(first, history[0]);
    }

    [Fact]
    public async Task RequestingAgain_PreservesEarlierVersionsUnchanged()
    {
        var id = _f.ApproveWithDrafts();
        var first = (await _f.Proposing.ProposeAsync(id)).Proposal!;
        _f.Clock.Now = _f.Clock.Now.AddHours(1);

        await _f.Proposing.ProposeAsync(id);

        Assert.Equal(first, _f.Proposing.GetProposalHistory(id).Value![0]);
    }

    [Fact]
    public void WithNoProposal_LatestIsASuccessfulNull_AndHistoryIsEmpty()
    {
        var id = _f.ApproveWithDrafts();

        var latest = _f.Proposing.GetLatestProposal(id);

        Assert.True(latest.IsSuccess);
        Assert.Null(latest.Value);
        Assert.Empty(_f.Proposing.GetProposalHistory(id).Value!);
    }

    [Fact]
    public void AnUnknownChangeRequest_IsReportedOnTheIdField_ForBothReads()
    {
        var unknown = Guid.NewGuid();

        var latest = _f.Proposing.GetLatestProposal(unknown);
        var history = _f.Proposing.GetProposalHistory(unknown);

        Assert.False(latest.IsSuccess);
        Assert.Equal("ChangeRequestId", Assert.Single(latest.Errors).FieldName);
        Assert.False(history.IsSuccess);
        Assert.Equal("ChangeRequestId", Assert.Single(history.Errors).FieldName);
    }

    [Fact]
    public async Task Reads_AreNotAffectedByAnotherChangeRequestsProposals()
    {
        var a = _f.ApproveWithDrafts();
        var b = _f.ApproveWithDrafts();
        await _f.Proposing.ProposeAsync(a);

        Assert.Empty(_f.Proposing.GetProposalHistory(b).Value!);
        Assert.Single(_f.Proposing.GetProposalHistory(a).Value!);
    }

    [Fact]
    public async Task OverlappingRequests_GetDistinctConsecutiveVersions_AndOneAuditEntryEach()
    {
        const int Requests = 12;
        var id = _f.ApproveWithDrafts();

        var results = await Task.WhenAll(Enumerable.Range(0, Requests).Select(_ => Task.Run(() => _f.Proposing.ProposeAsync(id))));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        var versions = _f.Proposing.GetProposalHistory(id).Value!.Select(p => p.Version).ToArray();
        Assert.Equal(Enumerable.Range(1, Requests), versions);
        Assert.Equal(Requests, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }

    [Fact]
    public async Task AReader_NeverSeesAProposalWithoutItsAuditEntry()
    {
        var id = _f.ApproveWithDrafts();
        var stop = false;
        var violations = 0;
        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                var proposals = _f.Proposing.GetProposalHistory(id).Value!.Count;
                var entries = _f.AuditCount(id, AuditEventType.ProposalGenerated);
                if (entries < proposals)
                    Interlocked.Increment(ref violations);
            }
        });

        for (var i = 0; i < 20; i++)
            await _f.Proposing.ProposeAsync(id);
        Volatile.Write(ref stop, true);
        await reader;

        Assert.Equal(0, violations);
    }
}
