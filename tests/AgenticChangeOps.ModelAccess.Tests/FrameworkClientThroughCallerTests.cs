using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// FR-007, SC-004: the shared caller over the framework client keeps the time limit, the size limit
/// and the category mapping, and still makes one request.
/// </summary>
public sealed class FrameworkClientThroughCallerTests
{
    private static ModelCaller Caller(RecordingTransport transport, TimeSpan? timeout = null, int maxChars = 1500)
    {
        var options = ClientHarness.Options(timeout, maxChars);
        return new ModelCaller(options, ClientHarness.Framework(transport, options: options));
    }

    private static async Task<GenerationFailureCategory> CategoryAsync(ModelCaller caller)
    {
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ex = await Assert.ThrowsAsync<DraftGenerationException>(
            () => caller.GetAnswerTextAsync(ClientHarness.SystemText, ClientHarness.UserText, safety.Token));
        return ex.Category;
    }

    [Fact]
    public async Task Success_ReturnsTheText_FromOneRequest()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{\"ok\":true}");

        var text = await Caller(transport).GetAnswerTextAsync(
            ClientHarness.SystemText, ClientHarness.UserText, CancellationToken.None);

        Assert.Equal("{\"ok\":true}", text);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task OutputTokenCap_IsTheCharacterLimitDividedByThree_RoundedUp()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{}");

        await Caller(transport, maxChars: 1000).GetAnswerTextAsync(
            ClientHarness.SystemText, ClientHarness.UserText, CancellationToken.None);

        var lines = WireSnapshot.BodyLines(Assert.Single(transport.Requests).Body);
        Assert.Contains(lines, l => l == "body.max_tokens = 334" || l == "body.max_completion_tokens = 334");
    }

    [Fact]
    public async Task AnswerSlowerThanTheTimeLimit_IsTimedOut_AfterOneRequest()
    {
        var transport = new RecordingTransport().HangsUntilCancelled();

        var category = await CategoryAsync(Caller(transport, timeout: TimeSpan.FromMilliseconds(150)));

        Assert.Equal(GenerationFailureCategory.TimedOut, category);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task TextOverTheLimit_IsResponseTooLarge()
    {
        var transport = new RecordingTransport().ReturnsCompletion(new string('x', 200));

        var category = await CategoryAsync(Caller(transport, maxChars: 100));

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, category);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task FinishReasonLength_IsResponseTooLarge()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{\"cut", finishReason: "length");

        var category = await CategoryAsync(Caller(transport));

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, category);
    }

    [Fact]
    public async Task BlankText_IsMalformedOutput()
    {
        var transport = new RecordingTransport().ReturnsCompletion("   ");

        var category = await CategoryAsync(Caller(transport));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, category);
    }

    [Fact]
    public async Task CallerCancellation_StaysCancellation_NotTimedOut()
    {
        var transport = new RecordingTransport().HangsUntilCancelled();
        var caller = Caller(transport, timeout: TimeSpan.FromSeconds(30));
        using var source = new CancellationTokenSource();

        var call = caller.GetAnswerTextAsync(ClientHarness.SystemText, ClientHarness.UserText, source.Token);
        source.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task ServiceFailure_IsUnreachable_AfterOneRequest()
    {
        var transport = new RecordingTransport().ReturnsStatus(503);

        var category = await CategoryAsync(Caller(transport));

        Assert.Equal(GenerationFailureCategory.Unreachable, category);
        Assert.Equal(1, transport.CallCount);
    }
}
