using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public class DraftGeneratorFailureTests
{
    private const string Endpoint = "https://synthetic-models.invalid/";
    private const string Deployment = "synthetic-deployment";

    private static readonly ChangeRequest Request = new(
        ChangeRequestId: Guid.NewGuid(),
        Title: "Move nightly report schedule",
        Description: "Move the nightly synthetic report job to a quieter window.",
        Requester: "Avery Quill",
        AffectedSystem: "Sample Billing Portal",
        Priority: Priority.Medium,
        ChangeType: ChangeType.ConfigChange,
        Status: ChangeRequestStatus.Submitted,
        SubmittedAt: new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Utc));

    private static (AzureOpenAIDraftGenerator Generator, FakeChatModelClient Client) Create(
        TimeSpan? timeout = null, int maxChars = 32000)
    {
        var client = new FakeChatModelClient();
        var options = new AzureOpenAIGeneratorOptions(
            new Uri(Endpoint), Deployment, timeout ?? TimeSpan.FromSeconds(30), maxChars);
        return (new AzureOpenAIDraftGenerator(options, client), client);
    }

    // The safety token only stops a test that would otherwise wait forever; it is far longer
    // than any configured time limit used here, so a correct generator never sees it fire.
    private static async Task<DraftGenerationException> Failure(AzureOpenAIDraftGenerator generator)
    {
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await Assert.ThrowsAsync<DraftGenerationException>(
            () => generator.GenerateAsync(Request, safety.Token));
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task CategoryThrownByTheClient_IsSurfacedUnchanged(GenerationFailureCategory category)
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.Throws(new DraftGenerationException(category));

        var ex = await Failure(generator);

        Assert.Equal(category, ex.Category);
    }

    [Fact]
    public async Task UnexpectedClientException_IsReportedAsUnreachableWithoutItsMessage()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.Throws(
            new InvalidOperationException($"failed calling {Endpoint} deployment {Deployment}"));

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.Unreachable, ex.Category);
        Assert.DoesNotContain(Endpoint, ex.Message);
        Assert.DoesNotContain(Deployment, ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task NoAnswerWithinTheTimeLimit_IsTimedOut()
    {
        var (generator, client) = Create(timeout: TimeSpan.FromMilliseconds(50));
        client.Behavior = FakeChatModelClient.DelaysUntilCancelled();

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.TimedOut, ex.Category);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesAsCancellationNotACategory()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.DelaysUntilCancelled();
        using var cts = new CancellationTokenSource();
        var pending = generator.GenerateAsync(Request, cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task AnswerLongerThanTheLimit_IsResponseTooLargeAndNoPartIsUsed()
    {
        var (generator, client) = Create(maxChars: 100);
        client.Behavior = FakeChatModelClient.ReturnsText(FakeChatModelClient.ValidAnswerJson());

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Fact]
    public async Task AnswerCutOffByTheService_IsResponseTooLarge()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.ReturnsText(FakeChatModelClient.ValidAnswerJson(), wasCutOff: true);

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Fact]
    public async Task EmptyAnswer_IsMalformedOutput()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.ReturnsText("   ");

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }

    [Fact]
    public async Task UnparseableAnswer_IsMalformedOutputAndTheAnswerTextIsNotInTheMessage()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.ReturnsText("synthetic-model-output-marker 12345");

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain("synthetic-model-output-marker", ex.Message);
    }

    [Fact]
    public async Task EveryFailure_MakesExactlyOneModelCall()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.Throws(new DraftGenerationException(GenerationFailureCategory.Unreachable));

        await Failure(generator);

        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task OutputTokenBound_IsDerivedFromTheCharacterLimit()
    {
        var (generator, client) = Create(maxChars: 3000);

        await generator.GenerateAsync(Request, CancellationToken.None);

        Assert.Equal(1000, client.MaxOutputTokens);
    }

    [Fact]
    public async Task FailureMessages_NeverContainPromptEndpointOrDeployment()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.ReturnsText("not json");

        var ex = await Failure(generator);

        Assert.DoesNotContain(Request.Title, ex.Message);
        Assert.DoesNotContain(Request.Description, ex.Message);
        Assert.DoesNotContain(Endpoint, ex.Message);
        Assert.DoesNotContain(Deployment, ex.Message);
    }
}
