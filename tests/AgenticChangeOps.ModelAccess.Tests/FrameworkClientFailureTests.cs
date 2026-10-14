using System.Net.Http;
using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// contracts/framework-client.md, "Failures": every failure ends as a category and nothing else, and
/// the category is the one the hand-written client gives for the same failure.
/// </summary>
public sealed class FrameworkClientFailureTests
{
    private const string Sentinel = "sentinel-failure-detail-7c1d";

    private static async Task<GenerationFailureCategory> CategoryAsync(IChatModelClient client)
    {
        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => client.CompleteAsync(
            ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None));
        return ex.Category;
    }

    public static IEnumerable<object[]> Scenarios() => new[]
    {
        new object[] { "408", (Func<RecordingTransport>)(() => new RecordingTransport().ReturnsStatus(408)), GenerationFailureCategory.TimedOut },
        new object[] { "400-content-filter", () => new RecordingTransport().ReturnsStatus(400, "{\"error\":{\"code\":\"content_filter\",\"message\":\"x\"}}"), GenerationFailureCategory.MalformedOutput },
        new object[] { "401", () => new RecordingTransport().ReturnsStatus(401), GenerationFailureCategory.Unreachable },
        new object[] { "403", () => new RecordingTransport().ReturnsStatus(403), GenerationFailureCategory.Unreachable },
        new object[] { "404", () => new RecordingTransport().ReturnsStatus(404), GenerationFailureCategory.Unreachable },
        new object[] { "429", () => new RecordingTransport().ReturnsStatus(429), GenerationFailureCategory.Unreachable },
        new object[] { "500", () => new RecordingTransport().ReturnsStatus(500), GenerationFailureCategory.Unreachable },
        new object[] { "503", () => new RecordingTransport().ReturnsStatus(503), GenerationFailureCategory.Unreachable },
        new object[] { "http-request-exception", () => new RecordingTransport().Throws(new HttpRequestException(Sentinel)), GenerationFailureCategory.Unreachable },
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Failure_MapsToTheExpectedCategory_AndMatchesTheHandWrittenClient(
        string name, Func<RecordingTransport> script, GenerationFailureCategory expected)
    {
        _ = name;
        var framework = await CategoryAsync(ClientHarness.Framework(script()));
        var handWritten = await CategoryAsync(ClientHarness.HandWritten(script()));

        Assert.Equal(expected, framework);
        Assert.Equal(handWritten, framework);
    }

    [Fact]
    public async Task TimeoutException_MapsLikeTheHandWrittenClient()
    {
        var framework = await CategoryAsync(
            ClientHarness.Framework(new RecordingTransport().Throws(new TimeoutException(Sentinel))));
        var handWritten = await CategoryAsync(
            ClientHarness.HandWritten(new RecordingTransport().Throws(new TimeoutException(Sentinel))));

        Assert.Equal(handWritten, framework);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesAsCancellation()
    {
        var transport = new RecordingTransport().HangsUntilCancelled();
        var client = ClientHarness.Framework(transport);
        using var source = new CancellationTokenSource();

        var call = client.CompleteAsync(
            ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, source.Token);
        source.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task Failure_CarriesNothingFromTheServiceOrTheTransport()
    {
        var transport = new RecordingTransport().Throws(new HttpRequestException(Sentinel + " https://sentinel-endpoint.invalid/"));
        var client = ClientHarness.Framework(transport);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => client.CompleteAsync(
            ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None));

        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(Sentinel, ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-endpoint", ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-deployment", ex.ToString(), StringComparison.Ordinal);
    }
}
