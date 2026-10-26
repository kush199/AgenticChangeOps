using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US4: overlapping requests for one change request keep versions whole.</summary>
public sealed class ReviewConcurrencyEndpointTests
{
    private const int Requests = 8;

    [Fact]
    public async Task OverlappingSuccessfulRequests_GiveVersionsExactlyOneToN_WithNoRepeat()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, Requests).Select(_ => host.RequestReviewAsync(id)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var returned = new List<int>();
        foreach (var response in responses)
            returned.Add((await response.ReadJsonAsync()).GetProperty("version").GetInt32());
        Assert.Equal(Enumerable.Range(1, Requests), returned.OrderBy(v => v));
        Assert.Equal(Enumerable.Range(1, Requests), host.Store.GetAgentOutputs<Review>(id).Select(r => r.Version));
        Assert.Equal(Requests, (await ReviewHttp.AuditEventsAsync(host, id)).Count(e => e == "ReviewGenerated"));
    }

    [Fact]
    public async Task OverlappingRequests_WhereSomeFail_LeaveNoGapInTheVersions()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var calls = 0;
        host.ReviewClient.Behavior = ct =>
            Interlocked.Increment(ref calls) % 2 == 0
                ? ScriptedChatModelClient.Throws(new DraftGenerationException(GenerationFailureCategory.TimedOut))(ct)
                : ScriptedChatModelClient.ReturnsText(ReviewHost.ValidReviewJson)(ct);

        var responses = await Task.WhenAll(Enumerable.Range(0, Requests).Select(_ => host.RequestReviewAsync(id)));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(Requests / 2, created);
        Assert.Equal(Requests / 2, responses.Count(r => r.StatusCode == HttpStatusCode.BadGateway));
        Assert.Equal(Enumerable.Range(1, created), host.Store.GetAgentOutputs<Review>(id).Select(r => r.Version));
        Assert.Equal(created, (await ReviewHttp.AuditEventsAsync(host, id)).Count(e => e == "ReviewGenerated"));
    }
}
