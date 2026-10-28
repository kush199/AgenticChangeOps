using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US3: retrieval changes nothing and leaks nothing.</summary>
public sealed class ReviewRetrievalReadOnlyTests
{
    [Fact]
    public async Task AnyNumberOfRetrievals_ChangeNothing()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 2);
        var reviews = host.Store.GetAgentOutputs<Review>(id);
        var audit = await host.AuditJsonAsync(id);
        var status = await ReviewHttp.StatusAsync(host, id);
        var workflow = await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync();
        var modelCalls = host.ReviewClient.CallCount;

        for (var i = 0; i < 3; i++)
        {
            await host.GetLatestReviewAsync(id);
            await host.GetReviewHistoryAsync(id);
            await host.GetLatestReviewAsync(Guid.NewGuid());
            await host.GetReviewHistoryAsync("nope");
        }

        Assert.Equal(reviews, host.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(audit, await host.AuditJsonAsync(id));
        Assert.Equal(status, await ReviewHttp.StatusAsync(host, id));
        Assert.Equal(workflow, await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync());
        Assert.Equal(modelCalls, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task ReadsDuringInFlightRequests_OnlyEverSeeWholeReviews_WithConsecutiveVersions()
    {
        const int requests = 8;
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var writers = Task.WhenAll(Enumerable.Range(0, requests).Select(_ => host.RequestReviewAsync(id)));
        var observed = new List<int[]>();
        while (!writers.IsCompleted)
        {
            var history = await host.GetReviewHistoryAsync(id);
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            var items = (await history.ReadJsonAsync()).EnumerateArray().ToArray();
            Assert.All(items, item =>
            {
                Assert.True(item.TryGetProperty("findings", out _));
                Assert.True(item.TryGetProperty("systemCheck", out _));
                Assert.True(item.TryGetProperty("advisoryNote", out _));
            });
            observed.Add(items.Select(i => i.GetProperty("version").GetInt32()).ToArray());

            var latest = await host.GetLatestReviewAsync(id);
            Assert.True(latest.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
        }
        await writers;

        Assert.All(observed, versions => Assert.Equal(Enumerable.Range(1, versions.Length), versions));
        var final = (await (await host.GetReviewHistoryAsync(id)).ReadJsonAsync()).EnumerateArray()
            .Select(i => i.GetProperty("version").GetInt32());
        Assert.Equal(Enumerable.Range(1, requests), final);
    }

    [Fact]
    public async Task NoRetrievalWritesAWarning_AndNothingSensitiveAppearsAnywhere()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 1);
        var empty = await host.Client.SubmitAsync();
        var before = host.Logs.Lines.Count;

        var responses = new List<HttpResponseMessage>
        {
            await host.GetLatestReviewAsync(id), await host.GetReviewHistoryAsync(id),
            await host.GetLatestReviewAsync(empty), await host.GetReviewHistoryAsync(empty),
            await host.GetLatestReviewAsync(Guid.NewGuid()), await host.GetReviewHistoryAsync("nope")
        };

        var texts = new List<string>();
        foreach (var response in responses)
        {
            texts.Add(await response.Content.ReadAsStringAsync());
            texts.Add(string.Join("|", response.Headers.Select(h => h.Key + "=" + string.Join(",", h.Value))));
        }
        var newLines = host.Logs.Lines.Skip(before).ToArray();
        texts.AddRange(newLines);

        Assert.All(texts, text =>
        {
            Assert.DoesNotContain(ReviewHost.Endpoint, text);
            Assert.DoesNotContain(ReviewHost.Deployment, text);
            Assert.DoesNotContain("   at ", text);
        });
        Assert.DoesNotContain(newLines, l => l.Contains("Review request stored nothing", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
    }
}
