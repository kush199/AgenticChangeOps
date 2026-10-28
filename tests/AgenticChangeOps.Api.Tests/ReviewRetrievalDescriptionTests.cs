using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US5: the published description lists both retrieval operations and what they can answer.</summary>
public sealed class ReviewRetrievalDescriptionTests
{
    private static async Task<JsonElement> PathAsync(string path)
    {
        using var host = new ReviewHost();
        var doc = await (await host.Client.GetAsync("/openapi/v1.json")).ReadJsonAsync();
        return doc.GetProperty("paths").GetProperty(path).Clone();
    }

    [Fact]
    public async Task TheLatestOperation_IsListed_WithItsResponses()
    {
        var get = (await PathAsync("/change-requests/{id}/reviews/latest")).GetProperty("get");

        Assert.Equal("GetLatestReview", get.GetProperty("operationId").GetString());
        Assert.Equal(new[] { "200", "400", "404" },
            get.GetProperty("responses").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task TheHistoryOperation_IsListed_WithItsResponses()
    {
        var path = await PathAsync("/change-requests/{id}/reviews");
        var get = path.GetProperty("get");

        Assert.Equal("GetReviewHistory", get.GetProperty("operationId").GetString());
        Assert.Equal(new[] { "200", "400", "404" },
            get.GetProperty("responses").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal("RequestReview", path.GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(new[] { "get", "post" }, path.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task NoOtherReviewPath_IsAdded()
    {
        using var host = new ReviewHost();
        var doc = await (await host.Client.GetAsync("/openapi/v1.json")).ReadJsonAsync();

        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name)
            .Where(n => n.Contains("review", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "/change-requests/{id}/reviews", "/change-requests/{id}/reviews/latest" }, paths);
    }
}
