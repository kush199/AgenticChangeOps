using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// No model text, endpoint, deployment name or credential may travel in any result. The service
/// takes no logger; log output is asserted at the HTTP level in the API tests (task T033).
/// </summary>
public sealed class DesignHygieneTests
{
    private const string FakeEndpoint = "https://synthetic-models.invalid/openai";
    private const string FakeDeployment = "synthetic-deployment-x";
    private const string FakeKey = "sk-synthetic-0000000000000000";

    private readonly DesigningTestFixture _f = new();

    private static void AssertNoLeak(DesignDraftingResult result)
    {
        var everything = string.Join(
            "\n", result.Errors.Select(e => e.FieldName + ": " + e.Message)) + "\n" + result.FailureCategory;
        Assert.DoesNotContain(FakeEndpoint, everything);
        Assert.DoesNotContain(FakeDeployment, everything);
        Assert.DoesNotContain(FakeKey, everything);
    }

    [Fact]
    public async Task GeneratorExceptionCarryingEndpointAndKey_LeaksNeitherIntoTheResult()
    {
        var id = _f.ApproveWithBasis();
        _f.Generator.Behavior = FakeDesignGenerator.Throws(new HttpRequestException(
            $"call to {FakeEndpoint} deployment {FakeDeployment} failed, key {FakeKey}"));

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        AssertNoLeak(result);
    }

    [Fact]
    public async Task ModelTextInUnusableContent_IsNeverForwardedInErrors()
    {
        var id = _f.ApproveWithBasis();
        _f.Generator.Behavior = FakeDesignGenerator.Returns(new DesignContent(
            ApproachSummary: $"see {FakeEndpoint} {FakeKey}",
            AffectedComponents: Array.Empty<string>(),
            ImpactAndRisks: new[] { FakeDeployment },
            Dependencies: Array.Empty<string>(),
            RollbackConsiderations: new[] { "ok" },
            OpenQuestions: Array.Empty<string>()));

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        AssertNoLeak(result);
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignGenerated));
    }

    [Fact]
    public async Task RealGeneratorOverAFailingClient_ProducesACategoryOnlyResultWithNoClientMessage()
    {
        var client = new FakeChatModelClient();
        client.Behavior = FakeChatModelClient.Throws(new InvalidOperationException(
            $"{FakeEndpoint} {FakeDeployment} {FakeKey}"));
        var options = new AzureOpenAIGeneratorOptions(
            new Uri(FakeEndpoint), FakeDeployment, TimeSpan.FromSeconds(30), 32000);
        var service = new DesignDraftingService(
            _f.Store, _f.Clock, _f.Ids, _f.Basis,
            new AzureOpenAIDesignGenerator(options, client), new DesignContentValidator());
        var id = _f.ApproveWithBasis();

        var result = await service.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.Unreachable, result.FailureCategory);
        AssertNoLeak(result);
        Assert.Empty(_f.Store.GetDesigns(id));
    }
}
