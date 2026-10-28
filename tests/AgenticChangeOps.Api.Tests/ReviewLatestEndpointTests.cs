using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US1: the latest review is the stored review with the highest version, exactly as stored.</summary>
public sealed class ReviewLatestEndpointTests
{
    [Fact]
    public async Task WithTwoReviews_TheLatestIsVersion2_ExactlyAsStored()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 2);

        var response = await host.GetLatestReviewAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var stored = host.Store.GetAgentOutputs<Review>(id);
        Assert.Equal(2, stored.Count);
        ReviewHttp.AssertMatchesStored(await response.ReadJsonAsync(), stored[1]);
    }

    [Fact]
    public async Task WithOneReview_ThatReviewIsReturned()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 1);

        var json = await (await host.GetLatestReviewAsync(id)).ReadJsonAsync();

        ReviewHttp.AssertMatchesStored(json, Assert.Single(host.Store.GetAgentOutputs<Review>(id)));
        Assert.Equal(1, json.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task ARequestBody_IsIgnored()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 1);

        var response = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/change-requests/{id}/reviews/latest")
        {
            Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json")
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheBody_UsesCamelCaseMembers_AndStringEnums()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 1);

        var json = await (await host.GetLatestReviewAsync(id)).ReadJsonAsync();

        foreach (var name in new[] { "reviewId", "changeRequestId", "version", "requirementsVersion", "designVersion",
                     "proposalVersion", "generatedAt", "status", "agentName", "systemCheck", "findings", "openQuestions", "advisoryNote" })
            Assert.True(json.TryGetProperty(name, out _), name);
        Assert.Equal("Draft", json.GetProperty("status").GetString());
    }
}
