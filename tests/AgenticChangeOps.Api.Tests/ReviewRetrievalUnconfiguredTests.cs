using System.Net;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>Retrieval only reads what is stored, so it never depends on reviewing being configured.</summary>
public sealed class ReviewRetrievalUnconfiguredTests
{
    private static readonly Dictionary<string, string?> Unconfigured = new()
    {
        [ReviewSettings.MaxFindingsVariable] = "lots-of-garbage"
    };

    [Fact]
    public async Task StoredReviews_AreStillReturned_ByBothRoutes()
    {
        using var host = new ReviewHost(settings: new(Unconfigured));
        var id = await host.Client.SubmitAsync();
        var seeded = host.SeedReviews(id, 2);

        var latest = await host.GetLatestReviewAsync(id);
        var history = await host.GetReviewHistoryAsync(id);

        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        ReviewHttp.AssertMatchesStored(await latest.ReadJsonAsync(), seeded[1]);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(new[] { 1, 2 },
            (await history.ReadJsonAsync()).EnumerateArray().Select(i => i.GetProperty("version").GetInt32()));
        Assert.Equal(0, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task TheOtherOutcomes_AreTheSameAsOnAConfiguredServer()
    {
        using var host = new ReviewHost(settings: new(Unconfigured));
        var id = await host.Client.SubmitAsync();

        await ReviewHttp.AssertErrorAsync(await host.GetLatestReviewAsync(id), HttpStatusCode.NotFound, "no-review");
        Assert.Empty((await (await host.GetReviewHistoryAsync(id)).ReadJsonAsync()).EnumerateArray());
        await ReviewHttp.AssertErrorAsync(await host.GetLatestReviewAsync(Guid.NewGuid()), HttpStatusCode.NotFound, "not-found");
        await ReviewHttp.AssertErrorAsync(await host.GetLatestReviewAsync("nope"), HttpStatusCode.BadRequest, "invalid-identifier");
        Assert.Equal(0, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task WithModelSettingsMissing_StoredReviewsAreStillReturned()
    {
        using var host = new ReviewHost(toolsOn: false, modelConfigured: false);
        var id = await host.Client.SubmitAsync();
        host.SeedReviews(id, 1);

        Assert.Equal(HttpStatusCode.OK, (await host.GetLatestReviewAsync(id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetReviewHistoryAsync(id)).StatusCode);
    }
}
