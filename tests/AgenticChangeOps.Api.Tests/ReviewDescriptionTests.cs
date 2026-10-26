using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US5: the published description lists the one new operation with its possible responses.</summary>
public sealed class ReviewDescriptionTests
{
    private const string ReviewPath = "/change-requests/{id}/reviews";

    [Fact]
    public async Task TheDescription_ListsTheReviewOperation_WithTheDocumentedResponses()
    {
        using var host = new ReviewHost();

        var doc = await (await host.Client.GetAsync("/openapi/v1.json")).ReadJsonAsync();
        var post = doc.GetProperty("paths").GetProperty(ReviewPath).GetProperty("post");

        var codes = post.GetProperty("responses").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "201", "400", "404", "409", "500", "502", "503" }, codes);
        Assert.Equal("RequestReview", post.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task TheReviewPath_HasNoOtherOperation_AndNoRetrievalPathExists()
    {
        using var host = new ReviewHost();

        var doc = await (await host.Client.GetAsync("/openapi/v1.json")).ReadJsonAsync();
        var paths = doc.GetProperty("paths");

        var methods = paths.GetProperty(ReviewPath).EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "post" }, methods);
        Assert.Equal(
            new[] { ReviewPath },
            paths.EnumerateObject().Select(p => p.Name).Where(n => n.Contains("review", StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    [Theory]
    [InlineData("/change-requests/{0}/reviews/latest")]
    [InlineData("/reviews")]
    public async Task NoRetrievalRoute_Exists(string template)
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.Client.GetAsync(string.Format(template, id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("route-not-found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetOnTheReviewRoute_IsMethodNotAllowed()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.Client.GetAsync($"/change-requests/{id}/reviews");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task TheDescription_DocumentsTheNewErrorCodes_AndLeaksNoConfiguration()
    {
        using var host = new ReviewHost();

        var text = await (await host.Client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(ReviewHost.Endpoint, text);
        Assert.DoesNotContain(ReviewHost.Deployment, text);
    }

    [Fact]
    public async Task TheWorkflowStatusBody_StillHasNoReviewMember()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        (await host.RequestReviewAsync(id)).EnsureSuccessStatusCode();

        var body = await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("review", body, StringComparison.OrdinalIgnoreCase);
    }
}
