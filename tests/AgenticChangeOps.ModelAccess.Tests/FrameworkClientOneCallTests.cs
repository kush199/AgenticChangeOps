using System.Net.Http;
using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>FR-002, FR-003: one request per call, no retry of any kind, no tools offered.</summary>
public sealed class FrameworkClientOneCallTests
{
    private static async Task RunIgnoringFailureAsync(AgentFrameworkChatModelClient client)
    {
        try
        {
            await client.CompleteAsync(
                ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None);
        }
        catch (DraftGenerationException)
        {
            // The failure itself is covered elsewhere; here only the number of requests matters.
        }
    }

    [Fact]
    public async Task Success_IsExactlyOneRequest()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{}");

        await RunIgnoringFailureAsync(ClientHarness.Framework(transport));

        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task FailureStatus_IsStillExactlyOneRequest_NoRetry(int status)
    {
        var transport = new RecordingTransport().Always(_ =>
            Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)status)
            {
                Content = new StringContent("{}"),
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) }
            }));

        await RunIgnoringFailureAsync(ClientHarness.Framework(transport));

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task TransportFailure_IsStillExactlyOneRequest_NoRetry()
    {
        var transport = new RecordingTransport().Always(_ => throw new HttpRequestException("synthetic"));

        await RunIgnoringFailureAsync(ClientHarness.Framework(transport));

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task NoToolsAreOffered_NeitherAsToolsNorAsFunctions()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{}");

        await RunIgnoringFailureAsync(ClientHarness.Framework(transport));

        var body = Assert.Single(transport.Requests).Body;
        Assert.DoesNotContain("\"tools\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tool_choice\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"functions\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"parallel_tool_calls\"", body, StringComparison.Ordinal);
    }
}
