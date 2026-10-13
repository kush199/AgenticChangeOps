using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public class ModelFailureMapperTests
{
    // Local stand-ins named like the identity library's exceptions, so no cloud package is
    // needed to exercise the mapping.
    private sealed class CredentialUnavailableException(string message) : Exception(message);
    private sealed class AuthenticationFailedException(string message) : Exception(message);

    [Theory]
    [InlineData("credential")]
    [InlineData("authentication")]
    public async Task IdentityFailures_AreUnreachableAndRevealNoIdentityDetail(string kind)
    {
        const string detail = "identity-sentinel-detail";
        Exception failure = kind == "credential"
            ? new CredentialUnavailableException(detail)
            : new AuthenticationFailedException(detail);
        Assert.Equal(GenerationFailureCategory.Unreachable, ModelFailureMapper.FromException(failure));

        var client = new FakeChatModelClient { Behavior = FakeChatModelClient.Throws(failure) };
        var options = new AzureOpenAIGeneratorOptions(
            new Uri("https://synthetic-models.invalid/"), "synthetic-deployment", TimeSpan.FromSeconds(30), 32000);
        var generator = new AzureOpenAIDraftGenerator(options, client);
        var request = new ChangeRequest(
            Guid.NewGuid(), "Title", "Description", "Requester", "System",
            Priority.Low, ChangeType.Bugfix, ChangeRequestStatus.Submitted, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(
            () => generator.GenerateAsync(request, CancellationToken.None));

        Assert.Equal(GenerationFailureCategory.Unreachable, ex.Category);
        Assert.DoesNotContain(detail, ex.Message);
        Assert.DoesNotContain("CredentialUnavailableException", ex.Message);
        Assert.DoesNotContain("AuthenticationFailedException", ex.Message);
        Assert.Null(ex.InnerException);
    }
}
