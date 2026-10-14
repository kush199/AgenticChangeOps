using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>contracts/framework-client.md, "Response".</summary>
public sealed class FrameworkClientResultTests
{
    private static Task<ChatModelResponse> CompleteAsync(RecordingTransport transport) =>
        ClientHarness.Framework(transport).CompleteAsync(
            ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None);

    [Fact]
    public async Task NormalCompletion_ReturnsTextVerbatim_NotCutOff()
    {
        var transport = new RecordingTransport().ReturnsCompletion("  {\"a\": 1}\n");

        var response = await CompleteAsync(transport);

        Assert.Equal("  {\"a\": 1}\n", response.Text);
        Assert.False(response.WasCutOff);
    }

    [Fact]
    public async Task FinishReasonLength_IsReportedAsCutOff()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{\"partial\":", finishReason: "length");

        var response = await CompleteAsync(transport);

        Assert.True(response.WasCutOff);
        Assert.Equal("{\"partial\":", response.Text);
    }

    [Fact]
    public async Task FinishReasonContentFilter_IsMalformedOutput()
    {
        var transport = new RecordingTransport().ReturnsCompletion("", finishReason: "content_filter");

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => CompleteAsync(transport));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }

    [Fact]
    public async Task EmptyText_ComesBackAsEmptyText_ForTheCallerToClassify()
    {
        var transport = new RecordingTransport().ReturnsCompletion("");

        var response = await CompleteAsync(transport);

        Assert.Equal(string.Empty, response.Text);
        Assert.False(response.WasCutOff);
    }
}
