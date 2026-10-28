using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US3: refusals follow the conventions of the other retrieval operations.</summary>
public sealed class ReviewRetrievalRefusalTests
{
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("00000000-0000-0000-0000")]
    public async Task AMalformedId_Is400_OnBothRoutes(string segment)
    {
        using var host = new ReviewHost();

        await ReviewHttp.AssertErrorAsync(await host.GetLatestReviewAsync(segment), HttpStatusCode.BadRequest, "invalid-identifier");
        await ReviewHttp.AssertErrorAsync(await host.GetReviewHistoryAsync(segment), HttpStatusCode.BadRequest, "invalid-identifier");
    }

    [Fact]
    public async Task AnUnknownId_Is404NotFound_OnBothRoutes()
    {
        using var host = new ReviewHost();
        var id = Guid.NewGuid();

        await ReviewHttp.AssertErrorAsync(await host.GetLatestReviewAsync(id), HttpStatusCode.NotFound, "not-found");
        await ReviewHttp.AssertErrorAsync(await host.GetReviewHistoryAsync(id), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task AnExistingRequestWithNoReview_Is404NoReview_NotNotFound_ForTheLatest()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();

        var response = await host.GetLatestReviewAsync(id);

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.NotFound, "no-review");
        var json = await response.ReadJsonAsync();
        Assert.NotEqual("not-found", json.GetProperty("code").GetString());
        Assert.False(json.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task AStorageFailureWhileReading_KeepsTheExisting503Shape_OnBothRoutes()
    {
        using var factory = new ApiFactory(configureServices: services =>
            services.AddSingleton<IChangeRequestStore>(
                new ReviewReadFailingStore(new StorageUnavailableException(StorageFailureCategory.UnsupportedLayout))));
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        foreach (var route in new[] { $"/change-requests/{id}/reviews/latest", $"/change-requests/{id}/reviews" })
        {
            var response = await client.GetAsync(route);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var json = await response.ReadJsonAsync();
            Assert.Equal("storage-unavailable", json.GetProperty("code").GetString());
            Assert.Equal("UnsupportedLayout", json.GetProperty("category").GetString());
            var text = json.GetRawText();
            Assert.DoesNotContain("   at ", text);
            Assert.DoesNotContain("Exception", text);
        }
    }
}
