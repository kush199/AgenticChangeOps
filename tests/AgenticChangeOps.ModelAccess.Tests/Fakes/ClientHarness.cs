using Azure.AI.OpenAI;

namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>
/// Builds the framework client over a fake transport. Only the transport is supplied through the
/// client options: the one-attempt policy must come from the client itself, so the retry tests
/// prove the client's own behaviour rather than the test's.
/// </summary>
internal static class ClientHarness
{
    public const string SystemText = "Synthetic system instruction. Reply with a JSON object.";
    public const string UserText = "Synthetic user content with a short change description.";
    public const int MaxTokens = 500;

    public static AzureOpenAIGeneratorOptions Options(TimeSpan? timeout = null, int maxChars = 1500) =>
        new(new Uri("https://sentinel-endpoint.invalid/"), "sentinel-deployment",
            timeout ?? TimeSpan.FromSeconds(30), maxChars);

    public static AgentFrameworkChatModelClient Framework(
        RecordingTransport transport, FakeTokenCredential? credential = null, AzureOpenAIGeneratorOptions? options = null) =>
        new(options ?? Options(), new AzureOpenAIClientOptions { Transport = transport.AsPipelineTransport() },
            credential ?? new FakeTokenCredential());

    /// <summary>
    /// The hand-written client, configured as its public constructor configures it. Built in the one
    /// test file allowed to name that client (see HandWrittenClientWireBaselineTests).
    /// </summary>
    public static IChatModelClient HandWritten(
        RecordingTransport transport, FakeTokenCredential? credential = null, AzureOpenAIGeneratorOptions? options = null) =>
        HandWrittenClientWireBaselineTests.CreateHandWritten(
            options ?? Options(),
            new AzureOpenAIClientOptions
            {
                RetryPolicy = new System.ClientModel.Primitives.ClientRetryPolicy(0),
                Transport = transport.AsPipelineTransport()
            },
            credential ?? new FakeTokenCredential());
}
