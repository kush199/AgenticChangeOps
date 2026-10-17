using System.Net.Http;
using Azure.AI.OpenAI;
using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// FR-013, FR-020: what the tool-capable client does on the wire. Each model call is exactly one
/// HTTP request, nothing is retried, the three session tools are offered by name, a tool result goes
/// back as a tool message, and failures end as a category and nothing else. Uses the fake transport
/// only; no network.
/// </summary>
public sealed class FrameworkToolClientWireTests
{
    private const string Sentinel = "sentinel-failure-detail-9b2e";

    private static AgentFrameworkToolChatModelClient Client(RecordingTransport transport) =>
        new(ClientHarness.Options(),
            new AzureOpenAIClientOptions { Transport = transport.AsPipelineTransport() },
            new FakeTokenCredential());

    private static Task<ChatModelResponse> RunAsync(
        AgentFrameworkToolChatModelClient client, FakeToolSession session) =>
        client.CompleteAsync(ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, session, CancellationToken.None);

    private static string ToolCallCompletion(string tool, string argumentsJson) =>
        "{\"id\":\"chatcmpl-synthetic\",\"object\":\"chat.completion\",\"created\":1700000000,\"model\":\"synthetic-model\"," +
        "\"choices\":[{\"index\":0,\"finish_reason\":\"tool_calls\",\"message\":{\"role\":\"assistant\",\"content\":null," +
        "\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"" + tool + "\"," +
        "\"arguments\":" + System.Text.Json.JsonSerializer.Serialize(argumentsJson) + "}}]}}]," +
        "\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";

    [Fact]
    public async Task ASuccessfulAnswerWithNoToolUse_IsExactlyOneRequest_OfferingTheThreeToolsAndAskingForJson()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{}");

        var response = await RunAsync(Client(transport), new FakeToolSession());

        Assert.Equal("{}", response.Text);
        Assert.Equal(1, transport.CallCount);
        var body = Assert.Single(transport.Requests).Body;
        foreach (var tool in new[] { "list_files", "read_file", "search_text" })
            Assert.Contains($"\"name\":\"{tool}\"", body, StringComparison.Ordinal);
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("delete_file", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AToolRound_IsTwoRequests_AndTheToolResultGoesBackAsAToolMessage()
    {
        var transport = new RecordingTransport()
            .ReturnsStatus(200, ToolCallCompletion("read_file", "{\"path\":\"orders-service/src/OrderIntake\"}"))
            .ReturnsCompletion("{\"ok\":true}");
        var session = new FakeToolSession(result: (_, _) => "TOOL-RESULT-TEXT-0001");

        var response = await RunAsync(Client(transport), session);

        Assert.Equal("{\"ok\":true}", response.Text);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal(new[] { "read_file" }, session.Invoked);
        var second = transport.Requests[1].Body;
        Assert.Contains("\"role\":\"tool\"", second, StringComparison.Ordinal);
        Assert.Contains("TOOL-RESULT-TEXT-0001", second, StringComparison.Ordinal);
        Assert.DoesNotContain("TOOL-RESULT-TEXT-0001", transport.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModelThatNeverStopsAskingForTools_IsStoppedAtTheLimit_WithExactlyThatManyRequests()
    {
        var transport = new RecordingTransport()
            .Always(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallCompletion("read_file", "{\"path\":\"a\"}"), System.Text.Encoding.UTF8, "application/json")
            }));
        var session = new FakeToolSession(maxModelCalls: 3);

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(Client(transport), session));

        Assert.Equal(GenerationFailureCategory.LimitsExhausted, error.Category);
        Assert.Equal(3, transport.CallCount);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task AFailureStatus_IsExactlyOneRequest_NoRetry(int status)
    {
        var transport = new RecordingTransport().Always(_ =>
            Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)status)
            {
                Content = new StringContent("{}"),
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) }
            }));

        await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(Client(transport), new FakeToolSession()));

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ATransportFailure_IsExactlyOneRequest_NoRetry()
    {
        var transport = new RecordingTransport().Always(_ => throw new HttpRequestException(Sentinel));

        await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(Client(transport), new FakeToolSession()));

        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(408, GenerationFailureCategory.TimedOut)]
    [InlineData(401, GenerationFailureCategory.Unreachable)]
    [InlineData(429, GenerationFailureCategory.Unreachable)]
    [InlineData(503, GenerationFailureCategory.Unreachable)]
    public async Task FailureCategories_MatchThePlainFrameworkClient(int status, GenerationFailureCategory expected)
    {
        var withTools = await Assert.ThrowsAsync<DraftGenerationException>(() =>
            RunAsync(Client(new RecordingTransport().ReturnsStatus(status)), new FakeToolSession()));
        var plain = await Assert.ThrowsAsync<DraftGenerationException>(() =>
            ClientHarness.Framework(new RecordingTransport().ReturnsStatus(status)).CompleteAsync(
                ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None));

        Assert.Equal(expected, withTools.Category);
        Assert.Equal(plain.Category, withTools.Category);
    }

    [Fact]
    public async Task AContentFilterRefusal_IsMalformedOutput()
    {
        var transport = new RecordingTransport().ReturnsStatus(400, "{\"error\":{\"code\":\"content_filter\",\"message\":\"x\"}}");

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(Client(transport), new FakeToolSession()));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, error.Category);
    }

    [Fact]
    public async Task ATimedOutModelCall_IsTimedOut_NotALimit()
    {
        var transport = new RecordingTransport().HangsUntilCancelled();
        var client = new AgentFrameworkToolChatModelClient(
            ClientHarness.Options(timeout: TimeSpan.FromMilliseconds(200)),
            new AzureOpenAIClientOptions { Transport = transport.AsPipelineTransport() },
            new FakeTokenCredential());

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(client, new FakeToolSession(timeLimit: TimeSpan.FromSeconds(30))));

        Assert.Equal(GenerationFailureCategory.TimedOut, error.Category);
    }

    [Fact]
    public async Task NoFailure_CarriesTheEndpointDeploymentTransportDetailOrToolText()
    {
        var transport = new RecordingTransport().Always(_ => throw new HttpRequestException(Sentinel + " sentinel-endpoint sentinel-deployment"));

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(Client(transport), new FakeToolSession()));

        foreach (var forbidden in new[] { Sentinel, "sentinel-endpoint", "sentinel-deployment", "https://" })
            Assert.DoesNotContain(forbidden, error.Message);
        Assert.Null(error.InnerException);
    }
}
