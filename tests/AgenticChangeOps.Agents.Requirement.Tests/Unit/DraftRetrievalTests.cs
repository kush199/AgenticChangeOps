using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public sealed class DraftRetrievalTests
{
    private readonly DraftingTestFixture _f = new();

    [Fact]
    public async Task GetLatestDraft_ThreeDrafts_ReturnsVersionThree()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        await _f.Drafting.DraftAsync(id);
        await _f.Drafting.DraftAsync(id);

        var result = _f.Drafting.GetLatestDraft(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Version);
    }

    [Fact]
    public async Task GetDraftHistory_ReturnsAllVersionsInAscendingOrder()
    {
        var id = _f.SubmitSynthetic();
        var returned = new List<RequirementsDraft>();
        for (var i = 0; i < 3; i++)
            returned.Add((await _f.Drafting.DraftAsync(id)).Draft!);

        var result = _f.Drafting.GetDraftHistory(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(returned, result.Value);
        Assert.Equal(new[] { 1, 2, 3 }, result.Value!.Select(d => d.Version));
    }

    [Fact]
    public void Retrieval_ExistingRequestWithNoDrafts_IsSuccessWithNullLatestAndEmptyHistory()
    {
        var id = _f.SubmitSynthetic();

        var latest = _f.Drafting.GetLatestDraft(id);
        var history = _f.Drafting.GetDraftHistory(id);

        Assert.True(latest.IsSuccess);
        Assert.Null(latest.Value);
        Assert.True(history.IsSuccess);
        Assert.Empty(history.Value!);
    }

    [Fact]
    public void Retrieval_UnknownId_ReturnsNotFoundErrorNamingTheId()
    {
        var unknown = Guid.NewGuid();

        var latest = _f.Drafting.GetLatestDraft(unknown);
        var history = _f.Drafting.GetDraftHistory(unknown);

        foreach (var errors in new[] { latest.Errors, history.Errors })
        {
            var error = Assert.Single(errors);
            Assert.Equal("ChangeRequestId", error.FieldName);
            Assert.Contains(unknown.ToString(), error.Message);
        }
        Assert.False(latest.IsSuccess);
        Assert.False(history.IsSuccess);
    }

    [Fact]
    public async Task Retrieval_AfterRequestIsApproved_StillReturnsEarlierDrafts()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        _f.SeedStatus(id, ChangeRequestStatus.Approved);

        Assert.Equal(1, _f.Drafting.GetLatestDraft(id).Value!.Version);
        Assert.Single(_f.Drafting.GetDraftHistory(id).Value!);
    }

    [Fact]
    public async Task Retrieval_WhileDraftsAreBeingStored_NeverThrowsAndSeesGapFreeVersions()
    {
        var id = _f.SubmitSynthetic();
        using var go = new ManualResetEventSlim();
        var writersDone = false;

        // The reader starts first and polls until every writer has finished, so reads and
        // writes genuinely overlap.
        var reader = Task.Run(() =>
        {
            go.Wait();
            do
            {
                var history = _f.Drafting.GetDraftHistory(id).Value!;
                Assert.Equal(Enumerable.Range(1, history.Count), history.Select(d => d.Version));

                var latest = _f.Drafting.GetLatestDraft(id).Value;
                if (latest is not null)
                    Assert.True(latest.Version >= history.Count);
            }
            while (!Volatile.Read(ref writersDone));
        });

        var writers = Enumerable.Range(0, 300)
            .Select(_ => Task.Run(() =>
            {
                go.Wait();
                return _f.Drafting.DraftAsync(id);
            }))
            .ToArray();

        go.Set();
        await Task.WhenAll(writers);
        Volatile.Write(ref writersDone, true);
        await reader;

        Assert.Equal(300, _f.Drafting.GetDraftHistory(id).Value!.Count);
    }

    [Fact]
    public async Task Retrieval_NeverCallsTheGenerator()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        var callsBefore = _f.Generator.CallCount;

        _f.Drafting.GetLatestDraft(id);
        _f.Drafting.GetDraftHistory(id);

        Assert.Equal(callsBefore, _f.Generator.CallCount);
    }
}
