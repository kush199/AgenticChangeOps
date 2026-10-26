using System.Net;
using System.Text;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US1 over HTTP: the route exposes the stored review and nothing else.</summary>
public sealed class ReviewEndpointTests
{
    [Fact]
    public async Task AnEligibleRequest_Is201_WithTheStoredReview_AndNoLocation()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Location"));
        var body = await response.ReadJsonAsync();
        var stored = Assert.Single(host.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(stored.ReviewId, body.GetProperty("reviewId").GetGuid());
        Assert.Equal(id, body.GetProperty("changeRequestId").GetGuid());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal("ReviewerAgent", body.GetProperty("agentName").GetString());
        Assert.Equal(JsonValueKindArray, body.GetProperty("findings").ValueKind);
        Assert.Equal(JsonValueKindArray, body.GetProperty("openQuestions").ValueKind);
        Assert.True(body.TryGetProperty("systemCheck", out _));
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindArray = System.Text.Json.JsonValueKind.Array;

    [Fact]
    public async Task TheBody_HasNoRetrievalLink()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.RequestReviewAsync(id);

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/reviews/latest", text);
        Assert.DoesNotContain($"/change-requests/{id}/reviews", text);
        Assert.DoesNotContain(response.Headers, h => h.Key.Equals("Link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheBody_UsesCamelCaseMembers_AndStringEnums()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var body = await (await host.RequestReviewAsync(id)).ReadJsonAsync();

        foreach (var name in new[] { "reviewId", "changeRequestId", "version", "requirementsVersion", "designVersion",
                     "proposalVersion", "generatedAt", "status", "agentName", "systemCheck", "findings", "openQuestions", "advisoryNote" })
            Assert.True(body.TryGetProperty(name, out _), name);
        Assert.Equal(System.Text.Json.JsonValueKind.String, body.GetProperty("status").ValueKind);
        var finding = body.GetProperty("findings")[0];
        Assert.Equal(System.Text.Json.JsonValueKind.String, finding.GetProperty("category").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.String, finding.GetProperty("severity").ValueKind);
    }

    [Fact]
    public async Task ASecondRequest_IsVersionTwo_AndVersionOneIsUnchanged()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var first = await (await host.RequestReviewAsync(id)).ReadJsonAsync();
        var second = await (await host.RequestReviewAsync(id)).ReadJsonAsync();

        Assert.Equal(1, first.GetProperty("version").GetInt32());
        Assert.Equal(2, second.GetProperty("version").GetInt32());
        var stored = host.Store.GetAgentOutputs<Review>(id);
        Assert.Equal(new[] { 1, 2 }, stored.Select(r => r.Version).ToArray());
        Assert.Equal(first.GetProperty("reviewId").GetGuid(), stored[0].ReviewId);
    }

    [Fact]
    public async Task TheChangeRequestStatus_IsUnchanged_AndOneReviewGeneratedEntryIsAdded()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var before = await ReviewHttp.StatusAsync(host, id);

        (await host.RequestReviewAsync(id)).EnsureSuccessStatusCode();

        Assert.Equal(before, await ReviewHttp.StatusAsync(host, id));
        Assert.Equal(1, (await ReviewHttp.AuditEventsAsync(host, id)).Count(e => e == "ReviewGenerated"));
    }

    [Theory]
    [InlineData("{\"anything\":true}")]
    [InlineData("{ not json")]
    [InlineData("")]
    public async Task ARequestBody_IsIgnored(string raw)
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.Client.PostAsync(
            $"/change-requests/{id}/reviews", new StringContent(raw, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AnOversizedRequestBody_IsIgnoredToo()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.Client.PostAsync(
            $"/change-requests/{id}/reviews", new StringContent(new string('x', 4000), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task WithTheSampleFolderMissing_AndTheToolsOff_TheRouteStillGives201_WithTheCheckNotPerformed()
    {
        var missing = Path.Combine(Path.GetTempPath(), "changeops-review-route-gone-" + Guid.NewGuid().ToString("N"));
        using var host = new ReviewHost(toolsOn: false, sampleFolder: missing);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var check = (await response.ReadJsonAsync()).GetProperty("systemCheck");
        Assert.Equal("NotPerformed", check.GetProperty("status").GetString());
        Assert.Empty(check.GetProperty("edits").EnumerateArray());
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task TheErrorBodyShape_MatchesTheProposalOperations()
    {
        using var host = new ReviewHost();

        var review = await (await host.RequestReviewAsync("not-a-guid")).ReadJsonAsync();
        var proposal = await (await host.Client.PostAsync("/change-requests/not-a-guid/proposals", null)).ReadJsonAsync();

        Assert.Equal(
            proposal.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray(),
            review.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(proposal.GetProperty("code").GetString(), review.GetProperty("code").GetString());
        Assert.Equal(proposal.GetProperty("message").GetString(), review.GetProperty("message").GetString());
    }
}
