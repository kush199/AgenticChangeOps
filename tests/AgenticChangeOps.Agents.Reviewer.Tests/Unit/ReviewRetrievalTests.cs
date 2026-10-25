using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>US2: latest and history, versions preserved, concurrent requests get distinct versions.</summary>
public sealed class ReviewRetrievalTests
{
    private readonly ReviewTestFixture _f = new();

    [Fact]
    public async Task Latest_IsTheNewestVersion_AndHistoryIsInAscendingOrder()
    {
        var id = _f.ApproveWithBases();
        var first = (await _f.Reviewing.ReviewAsync(id)).Review!;
        var second = (await _f.Reviewing.ReviewAsync(id)).Review!;

        Assert.Equal(2, second.Version);
        Assert.Equal(second, _f.Reviewing.GetLatestReview(id).Value);
        var history = _f.Reviewing.GetReviewHistory(id).Value!;
        Assert.Equal(new[] { 1, 2 }, history.Select(r => r.Version));
        Assert.Equal(first, history[0]);
    }

    [Fact]
    public async Task RequestingAgain_PreservesEarlierVersionsUnchanged()
    {
        var id = _f.ApproveWithBases();
        var first = (await _f.Reviewing.ReviewAsync(id)).Review!;
        _f.Clock.Now = _f.Clock.Now.AddHours(1);
        _f.Generator.Behavior = FakeReviewGenerator.Returns(new ReviewContent(Array.Empty<ReviewFindingContent>(), new[] { "A different question?" }));

        var second = (await _f.Reviewing.ReviewAsync(id)).Review!;

        var history = _f.Reviewing.GetReviewHistory(id).Value!;
        Assert.Equal(first, history[0]);
        Assert.NotEqual(first, second);
        Assert.Equal(2, first.Findings.Count);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void WithNoReview_LatestIsASuccessfulNull_AndHistoryIsEmpty()
    {
        var id = _f.ApproveWithBases();

        var latest = _f.Reviewing.GetLatestReview(id);

        Assert.True(latest.IsSuccess);
        Assert.Null(latest.Value);
        Assert.True(_f.Reviewing.GetReviewHistory(id).IsSuccess);
        Assert.Empty(_f.Reviewing.GetReviewHistory(id).Value!);
    }

    [Fact]
    public void AnUnknownChangeRequest_IsReportedOnTheIdField_ForBothReads()
    {
        var unknown = Guid.NewGuid();

        var latest = _f.Reviewing.GetLatestReview(unknown);
        var history = _f.Reviewing.GetReviewHistory(unknown);

        Assert.False(latest.IsSuccess);
        Assert.Equal("ChangeRequestId", Assert.Single(latest.Errors).FieldName);
        Assert.False(history.IsSuccess);
        Assert.Equal("ChangeRequestId", Assert.Single(history.Errors).FieldName);
    }

    [Fact]
    public async Task Reads_AreNotAffectedByAnotherChangeRequestsReviews()
    {
        var a = _f.ApproveWithBases();
        var b = _f.ApproveWithBases();
        await _f.Reviewing.ReviewAsync(a);

        Assert.Empty(_f.Reviewing.GetReviewHistory(b).Value!);
        Assert.Single(_f.Reviewing.GetReviewHistory(a).Value!);
    }

    [Fact]
    public async Task OverlappingRequests_GetDistinctConsecutiveVersions_AndOneAuditEntryEach()
    {
        const int Requests = 12;
        var id = _f.ApproveWithBases();

        var results = await Task.WhenAll(Enumerable.Range(0, Requests).Select(_ => Task.Run(() => _f.Reviewing.ReviewAsync(id))));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        var versions = _f.Reviewing.GetReviewHistory(id).Value!.Select(r => r.Version).ToArray();
        Assert.Equal(Enumerable.Range(1, Requests), versions);
        Assert.Equal(Requests, _f.AuditCount(id, AuditEventType.ReviewGenerated));
    }

    [Fact]
    public async Task AReader_NeverSeesAReviewWithoutItsAuditEntry()
    {
        var id = _f.ApproveWithBases();
        var stop = false;
        var violations = 0;
        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                var reviews = _f.Reviewing.GetReviewHistory(id).Value!.Count;
                var entries = _f.AuditCount(id, AuditEventType.ReviewGenerated);
                if (entries < reviews)
                    Interlocked.Increment(ref violations);
            }
        });

        for (var i = 0; i < 20; i++)
            await _f.Reviewing.ReviewAsync(id);
        Volatile.Write(ref stop, true);
        await reader;

        Assert.Equal(0, violations);
    }
}
