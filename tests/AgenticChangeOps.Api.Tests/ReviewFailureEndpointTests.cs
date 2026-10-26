using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US3 over HTTP: a review the system will not accept, and every model-side failure, stores nothing and
/// returns a reason or a category only.
/// </summary>
public sealed class ReviewFailureEndpointTests
{
    private const string Hostile = "HOSTILE-MARKER-0001";

    private static async Task AssertNoLeakAsync(ReviewHost host, Guid id, HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync() + string.Join("\n", host.Logs.Lines)
            + await host.AuditJsonAsync(id);
        foreach (var marker in new[]
                 {
                     Hostile, ReviewHost.Endpoint, ReviewHost.Deployment, "sentinel-", "proposal.changes[",
                     "orders.page.size", host.SampleFolder
                 })
            Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> Rejections() => new[]
    {
        new object[] { "UnresolvedReference", ReviewHttp.Answer(reference: "proposal.changes[99]", statement: Hostile) },
        new object[] { "MissingReference", """
            { "findings": [ { "category": "UnmentionedRisk", "severity": "Low", "statement": "HOSTILE-MARKER-0001", "references": [] } ],
              "openQuestions": [] }
            """ },
        new object[] { "TooManyFindings", ReviewHttp.Answer(findings: 9, statement: Hostile) },
        new object[] { "TooLarge", ReviewHttp.Answer(findings: 5, statement: Hostile + new string('x', 1700)) }
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task ARejectedReview_Is502ReviewRejected_WithTheReasonOnly_AndStoresNothing(string reason, string answer)
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.ReturnsText(answer);

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("review-rejected", body.GetProperty("code").GetString());
        Assert.Equal(reason, body.GetProperty("category").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
        await ReviewHttp.AssertNothingStoredAsync(host, id);
        await AssertNoLeakAsync(host, id, response);
        Assert.Equal("Approved", await ReviewHttp.StatusAsync(host, id));
    }

    [Fact]
    public async Task OneBadReference_AmongValidFindings_RejectsTheWholeReview()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.ReturnsText("""
            { "findings": [
                { "category": "UnmentionedRisk", "severity": "Low", "statement": "valid", "references": ["proposal.changes[1]"] },
                { "category": "UnmentionedRisk", "severity": "Low", "statement": "dangling", "references": ["proposal.changes[99]"] } ],
              "openQuestions": [] }
            """);

        var response = await host.RequestReviewAsync(id);

        Assert.Equal("review-rejected", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        await ReviewHttp.AssertNothingStoredAsync(host, id);
    }

    public static IEnumerable<object[]> ModelFailures() => new[]
    {
        new object[] { "Unreachable", GenerationFailureCategory.Unreachable },
        new object[] { "TimedOut", GenerationFailureCategory.TimedOut },
        new object[] { "ResponseTooLarge", GenerationFailureCategory.ResponseTooLarge },
        new object[] { "MalformedOutput", GenerationFailureCategory.MalformedOutput }
    };

    [Theory]
    [MemberData(nameof(ModelFailures))]
    public async Task AModelFailure_Is502GenerationFailed_WithTheCategoryOnly_AndStoresNothing(
        string category, GenerationFailureCategory failure)
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.Throws(new DraftGenerationException(failure));

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", body.GetProperty("code").GetString());
        Assert.Equal(category, body.GetProperty("category").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
        await ReviewHttp.AssertNothingStoredAsync(host, id);
        await AssertNoLeakAsync(host, id, response);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{ \"openQuestions\": [] }")]
    public async Task AnUnusableAnswer_Is502_MalformedOutput_AndForwardsNoText(string text)
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.ReturnsText(text);

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", body.GetProperty("code").GetString());
        Assert.Equal("MalformedOutput", body.GetProperty("category").GetString());
        await ReviewHttp.AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task AnUnexpectedException_Is502_WithNoMessageAttached_AndNothingLeaks()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.Throws(
            new InvalidOperationException(ReviewHost.Endpoint + " " + ReviewHost.Deployment + " " + Hostile));

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("generation-failed", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        await ReviewHttp.AssertNothingStoredAsync(host, id);
        await AssertNoLeakAsync(host, id, response);
    }

    [Fact]
    public async Task AFailedRequest_DoesNotConsumeAVersion()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.Throws(new DraftGenerationException(GenerationFailureCategory.TimedOut));
        await host.RequestReviewAsync(id);
        host.ReviewClient.Behavior = ScriptedChatModelClient.ReturnsText(ReviewHost.ValidReviewJson);

        var body = await (await host.RequestReviewAsync(id)).ReadJsonAsync();

        Assert.Equal(1, body.GetProperty("version").GetInt32());
    }
}
