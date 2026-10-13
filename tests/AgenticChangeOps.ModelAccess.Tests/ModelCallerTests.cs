using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Proves, once, the single time-limited model call and its failure mapping that every
/// model-backed agent relies on (contracts/shared-api.md, ModelCaller).
/// </summary>
public class ModelCallerTests
{
    private const string Endpoint = "https://synthetic-models.invalid/";
    private const string Deployment = "synthetic-deployment";
    private const string LeakMarker = "synthetic-leak-marker-91f2";

    private static (ModelCaller Caller, ScriptedChatModelClient Client) Create(
        TimeSpan? timeout = null, int maxChars = 32000)
    {
        var client = new ScriptedChatModelClient();
        var options = new AzureOpenAIGeneratorOptions(
            new Uri(Endpoint), Deployment, timeout ?? TimeSpan.FromSeconds(30), maxChars);
        return (new ModelCaller(options, client), client);
    }

    // The safety token only stops a test that would otherwise wait forever; it is far longer
    // than any time limit used here, so a correct caller never sees it fire.
    private static async Task<DraftGenerationException> Failure(ModelCaller caller)
    {
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await Assert.ThrowsAsync<DraftGenerationException>(
            () => caller.GetAnswerTextAsync("system", "user", safety.Token));
    }

    [Fact]
    public async Task Success_ReturnsTheAnswerText_FromExactlyOneCall()
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText("the answer");

        var text = await caller.GetAnswerTextAsync("the system text", "the user text", CancellationToken.None);

        Assert.Equal("the answer", text);
        Assert.Equal(1, client.CallCount);
        Assert.Equal("the system text", client.SystemInstruction);
        Assert.Equal("the user text", client.UserContent);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(32000, 10667)]
    [InlineData(100, 34)]
    public async Task OutputBound_IsTheCharacterLimitDividedByThreeRoundedUp(int maxChars, int expectedTokens)
    {
        var (caller, client) = Create(maxChars: maxChars);
        client.Behavior = ScriptedChatModelClient.ReturnsText("x");

        await caller.GetAnswerTextAsync("system", "user", CancellationToken.None);

        Assert.Equal(expectedTokens, client.MaxOutputTokens);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task CategoryThrownByTheClient_IsSurfacedUnchanged_AndNotRetried(GenerationFailureCategory category)
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.Throws(new DraftGenerationException(category));

        var ex = await Failure(caller);

        Assert.Equal(category, ex.Category);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task UnexpectedClientException_IsReportedAsUnreachable_WithoutItsMessageTypeOrInnerException()
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.Throws(
            new InvalidOperationException($"failed at {Endpoint} using {Deployment} {LeakMarker}"));

        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.Unreachable, ex.Category);
        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(LeakMarker, ex.Message);
        Assert.DoesNotContain(Endpoint, ex.Message);
        Assert.DoesNotContain(Deployment, ex.Message);
        Assert.DoesNotContain(nameof(InvalidOperationException), ex.ToString());
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task NetworkException_IsReportedAsUnreachable()
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.Throws(new HttpRequestException(LeakMarker));

        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.Unreachable, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
    }

    [Fact]
    public async Task TimeoutExceptionFromTheClient_IsReportedAsTimedOut()
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.Throws(new TimeoutException(LeakMarker));

        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.TimedOut, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
    }

    [Fact]
    public async Task TimeLimitExpiry_IsReportedAsTimedOut()
    {
        var (caller, client) = Create(timeout: TimeSpan.FromMilliseconds(50));
        client.Behavior = ScriptedChatModelClient.DelaysUntilCancelled();

        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.TimedOut, ex.Category);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task CancellationByTheCaller_PropagatesAsCancellation_NotAsACategory()
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.DelaysUntilCancelled();
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => caller.GetAnswerTextAsync("system", "user", cancel.Token));
    }

    [Fact]
    public async Task AnswerCutOffByTheService_IsResponseTooLarge()
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText("partial", wasCutOff: true);

        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Fact]
    public async Task AnswerOverTheCharacterLimit_IsResponseTooLarge_AndAnswerAtTheLimitIsAccepted()
    {
        var (caller, client) = Create(maxChars: 20);

        client.Behavior = ScriptedChatModelClient.ReturnsText(new string('a', 20));
        Assert.Equal(new string('a', 20), await caller.GetAnswerTextAsync("s", "u", CancellationToken.None));

        client.Behavior = ScriptedChatModelClient.ReturnsText(new string('a', 21));
        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public async Task EmptyOrBlankAnswer_IsMalformedOutput(string text)
    {
        var (caller, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText(text);

        var ex = await Failure(caller);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }

    [Fact]
    public void NullOptions_AreRejectedAtConstruction()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new ModelCaller(null!, new ScriptedChatModelClient()));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void NullClient_IsRejectedAtConstruction()
    {
        var options = new AzureOpenAIGeneratorOptions(
            new Uri(Endpoint), Deployment, TimeSpan.FromSeconds(30), 32000);

        var ex = Assert.Throws<ArgumentNullException>(() => new ModelCaller(options, null!));

        Assert.Equal("client", ex.ParamName);
    }
}
