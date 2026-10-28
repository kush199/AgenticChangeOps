using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US2: the history lists every stored review, oldest first, each exactly as stored.</summary>
public sealed class ReviewHistoryEndpointTests
{
    [Fact]
    public async Task WithThreeReviews_AllAreReturned_InVersionOrder_ExactlyAsStored()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 3);

        var response = await host.GetReviewHistoryAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var items = (await response.ReadJsonAsync()).EnumerateArray().ToArray();
        var stored = host.Store.GetAgentOutputs<Review>(id);
        Assert.Equal(new[] { 1, 2, 3 }, items.Select(i => i.GetProperty("version").GetInt32()));
        for (var i = 0; i < 3; i++)
            ReviewHttp.AssertMatchesStored(items[i], stored[i]);
    }

    [Fact]
    public async Task Version1_IsUnchanged_AfterLaterVersionsExist()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 1);
        var first = (await (await host.GetReviewHistoryAsync(id)).ReadJsonAsync()).EnumerateArray().Single().GetRawText();

        await host.RequestReviewsAsync(id, 2);
        var later = (await (await host.GetReviewHistoryAsync(id)).ReadJsonAsync()).EnumerateArray().ToArray();

        Assert.Equal(3, later.Length);
        Assert.Equal(first, later[0].GetRawText());
    }

    [Fact]
    public async Task AnExistingRequestWithNoReview_GivesAnEmptyList_With200()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();

        var response = await host.GetReviewHistoryAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task ARequestBody_IsIgnored()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(id, 2);

        var response = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/change-requests/{id}/reviews")
        {
            Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json")
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task OtherRecordsOfTheSameChangeRequest_NeverAppearInEitherRoute()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync(); // requirements draft, design and proposal already stored
        await host.RequestReviewsAsync(id, 2);

        var history = (await (await host.GetReviewHistoryAsync(id)).ReadJsonAsync()).EnumerateArray().ToArray();
        var latest = await (await host.GetLatestReviewAsync(id)).ReadJsonAsync();

        Assert.Equal(new[] { 1, 2 }, history.Select(i => i.GetProperty("version").GetInt32()));
        Assert.All(history.Append(latest), item =>
        {
            Assert.Equal("ReviewerAgent", item.GetProperty("agentName").GetString());
            Assert.True(item.TryGetProperty("findings", out _));
            Assert.False(item.TryGetProperty("changes", out _));
            Assert.False(item.TryGetProperty("sections", out _));
        });
    }

    [Fact]
    public async Task ReviewsOfAnotherChangeRequest_NeverAppear()
    {
        using var host = new ReviewHost();
        var first = await host.ReadyForReviewAsync();
        await host.RequestReviewsAsync(first, 2);
        var second = await host.Client.SubmitAsync();

        var secondItems = (await (await host.GetReviewHistoryAsync(second)).ReadJsonAsync()).EnumerateArray().ToArray();
        var firstItems = (await (await host.GetReviewHistoryAsync(first)).ReadJsonAsync()).EnumerateArray().ToArray();

        Assert.Empty(secondItems);
        Assert.All(firstItems, i => Assert.Equal(first, i.GetProperty("changeRequestId").GetGuid()));
    }
}
