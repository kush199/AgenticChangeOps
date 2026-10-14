using AgenticChangeOps.ModelAccess.Tests.Fakes;
using Azure.AI.OpenAI;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Records what the hand-written client puts on the wire, so the package lift and the framework
/// client can both be compared with it (slice 011, research R-003). Uses a fake transport and a
/// fake credential: no network and no real identity. This is the only test file outside the live
/// folders that may name the client (see the Requirement tests' GeneratorConfigurationTests).
/// </summary>
public sealed class HandWrittenClientWireBaselineTests
{
    internal const string System = "Synthetic system instruction. Reply with a JSON object.";
    internal const string User = "Synthetic user content with a short change description.";
    internal const int MaxTokens = 500;

    internal static IChatModelClient CreateHandWritten(
        AzureOpenAIGeneratorOptions options, AzureOpenAIClientOptions clientOptions, Azure.Core.TokenCredential credential) =>
        new AzureOpenAIChatModelClient(options, clientOptions, credential);

    internal static AzureOpenAIGeneratorOptions Options() =>
        new(new Uri("https://sentinel-endpoint.invalid/"), "sentinel-deployment", TimeSpan.FromSeconds(30), 1500);

    [Fact]
    public async Task HandWrittenClient_SendsOneRequest_WithTheRecordedShape()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{\"ok\":true}");
        var credential = new FakeTokenCredential();
        var clientOptions = new AzureOpenAIClientOptions
        {
            RetryPolicy = new System.ClientModel.Primitives.ClientRetryPolicy(0),
            Transport = transport.AsPipelineTransport()
        };

        var client = new AzureOpenAIChatModelClient(Options(), clientOptions, credential);
        var response = await client.CompleteAsync(System, User, MaxTokens, CancellationToken.None);

        Assert.Equal("{\"ok\":true}", response.Text);
        Assert.False(response.WasCutOff);
        var request = Assert.Single(transport.Requests);
        WireSnapshot.VerifyGolden("hand-written-wire.txt", WireSnapshot.Describe(request));
    }
}
