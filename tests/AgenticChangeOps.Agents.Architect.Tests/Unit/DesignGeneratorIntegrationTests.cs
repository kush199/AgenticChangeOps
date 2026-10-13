using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignGeneratorIntegrationTests
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
        Status: ChangeRequestStatus.Approved,
        SubmittedAt: new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Utc));

    private static (AzureOpenAIDesignGenerator Generator, FakeChatModelClient Client) Create(
        TimeSpan? timeout = null, int maxChars = 32000)
    {
        var client = new FakeChatModelClient();
        var options = new AzureOpenAIGeneratorOptions(
            new Uri(Endpoint), Deployment, timeout ?? TimeSpan.FromSeconds(30), maxChars);
        return (new AzureOpenAIDesignGenerator(options, client), client);
    }

    // The safety token only stops a test that would otherwise wait forever.
    private static async Task<DraftGenerationException> Failure(AzureOpenAIDesignGenerator generator)
    {
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await Assert.ThrowsAsync<DraftGenerationException>(
            () => generator.GenerateAsync(Request, FakeRequirementsBasisSource.Basis(), safety.Token));
    }

    [Fact]
    public async Task ValidAnswer_IsParsedIntoDesignContentWithOneModelCall()
    {
        var (generator, client) = Create();

        var content = await generator.GenerateAsync(
            Request, FakeRequirementsBasisSource.Basis(), CancellationToken.None);

        ContentAssert.Equal(FakeDesignGenerator.ValidContent(), content);
        Assert.Equal(1, client.CallCount);
        Assert.Contains(DesignPromptBuilder.DataDelimiter, client.UserContent);
        Assert.True(client.MaxOutputTokens > 0);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task CategoryThrownByTheClient_IsSurfacedUnchangedWithoutRetry(GenerationFailureCategory category)
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.Throws(new DraftGenerationException(category));

        var ex = await Failure(generator);

        Assert.Equal(category, ex.Category);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task UnexpectedClientException_IsUnreachableWithoutItsMessage()
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
        var pending = generator.GenerateAsync(Request, FakeRequirementsBasisSource.Basis(), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task AnswerLongerThanTheLimit_IsResponseTooLarge()
    {
        var (generator, client) = Create(maxChars: 100);

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
    public async Task BlankAnswer_IsMalformedOutput()
    {
        var (generator, client) = Create();
        client.Behavior = FakeChatModelClient.ReturnsText("   ");

        var ex = await Failure(generator);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }

    [Fact]
    public async Task ServiceOverTheRealGenerator_StoresADraftFromAWellFormedAnswer()
    {
        var f = new DesigningTestFixture();
        var (generator, _) = Create();
        var service = new DesignDraftingService(
            f.Store, f.Clock, f.Ids, f.Basis, generator, new DesignContentValidator());
        var id = f.ApproveWithBasis(basisVersion: 2);

        var result = await service.DesignAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Draft!.RequirementsVersion);
        Assert.Equal(FakeDesignGenerator.ValidContent().ApproachSummary, result.Draft.ApproachSummary);
        Assert.Equal(1, f.AuditCount(id, AuditEventType.DesignGenerated));
    }
}
