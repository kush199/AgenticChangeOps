using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// A review request that stores nothing logs one line with the failure category, rejection reason or
/// refused rule names, and never a finding, reference, path, model text, endpoint, deployment name or
/// setting value. A successful request logs no such line.
/// </summary>
public sealed class ReviewLoggingTests
{
    private const string Hostile = "HOSTILE-LOG-MARKER-0123";

    private static string[] StoredNothingLines(ReviewHost host) =>
        host.Logs.Lines.Where(l => l.Contains("Review request stored nothing", StringComparison.Ordinal)).ToArray();

    private static void AssertNoContent(ReviewHost host)
    {
        var all = string.Join("\n", host.Logs.Lines);
        foreach (var marker in new[]
                 {
                     Hostile, ReviewHost.Endpoint, ReviewHost.Deployment, "proposal.changes[", "orders.page.size",
                     "Synthetic finding", host.SampleFolder
                 })
            Assert.DoesNotContain(marker, all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARejectedReview_LogsOneLine_WithTheReasonName_AndNoContent()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.ReturnsText(
            ReviewHttp.Answer(reference: "proposal.changes[99]", statement: Hostile));

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var line = Assert.Single(StoredNothingLines(host));
        Assert.Contains("UnresolvedReference", line);
        AssertNoContent(host);
    }

    [Fact]
    public async Task AModelFailure_LogsOneLine_WithTheCategoryName_AndNoContent()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ScriptedChatModelClient.Throws(
            new DraftGenerationException(GenerationFailureCategory.TimedOut));

        await host.RequestReviewAsync(id);

        var line = Assert.Single(StoredNothingLines(host));
        Assert.Contains("TimedOut", line);
        AssertNoContent(host);
    }

    [Fact]
    public async Task ARefusal_LogsOneLine_NamingTheRule_NotTheContent()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync(); // not Approved

        await host.RequestReviewAsync(id);

        var line = Assert.Single(StoredNothingLines(host));
        Assert.Contains("Status", line);
        AssertNoContent(host);
    }

    [Fact]
    public async Task ASuccessfulRequest_LogsNoStoredNothingLine_AndNoContent()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(StoredNothingLines(host));
        AssertNoContent(host);
    }

    [Fact]
    public async Task EveryResponseAndLog_CarriesNoSentinel_AcrossSuccessRefusalAndFailure()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var seen = new List<string>();

        seen.Add(await (await host.RequestReviewAsync(id)).Content.ReadAsStringAsync());
        host.ReviewClient.Behavior = ScriptedChatModelClient.Throws(new InvalidOperationException(ReviewHost.Endpoint + Hostile));
        seen.Add(await (await host.RequestReviewAsync(id)).Content.ReadAsStringAsync());
        seen.Add(await (await host.RequestReviewAsync("not-a-guid")).Content.ReadAsStringAsync());
        seen.Add(await (await host.RequestReviewAsync(Guid.NewGuid())).Content.ReadAsStringAsync());
        seen.Add(string.Join("\n", host.Logs.Lines));

        foreach (var text in seen)
            foreach (var marker in new[] { ReviewHost.Endpoint, ReviewHost.Deployment, Hostile, "sentinel-" })
                Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
    }
}
