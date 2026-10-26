using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US2: identifier (400), existence (404), configuration (503), then the review capability decides.
/// The earliest failing check wins and the model is never reached once one has failed.
/// </summary>
public sealed class ReviewCheckOrderTests
{
    private static readonly Dictionary<string, string?> Unconfigured = new()
    {
        [ReviewSettings.MaxFindingsVariable] = "lots-of-garbage"
    };

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("00000000-0000-0000-0000")]
    public async Task AMalformedId_Is400_EvenWhenReviewingIsNotConfigured(string segment)
    {
        using var host = new ReviewHost(settings: new(Unconfigured));

        var response = await host.RequestReviewAsync(segment);

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid-identifier");
        Assert.Equal(0, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task AnUnknownId_Is404_EvenWhenReviewingIsNotConfigured()
    {
        using var host = new ReviewHost(settings: new(Unconfigured));

        var response = await host.RequestReviewAsync(Guid.NewGuid());

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.NotFound, "not-found");
        Assert.Equal(0, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task AnUnknownId_Is404_OnAConfiguredServerToo()
    {
        using var host = new ReviewHost();

        var response = await host.RequestReviewAsync(Guid.NewGuid());

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task AnIneligibleRequest_OnAnUnconfiguredServer_Is503NotConfigured_NotAConflict()
    {
        using var host = new ReviewHost(settings: new(Unconfigured));
        var id = await host.Client.SubmitAsync(); // exists, but not Approved

        var response = await host.RequestReviewAsync(id);

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "review-not-configured");
        Assert.Equal(0, host.ReviewClient.CallCount);
        await ReviewHttp.AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task AnEligibleRequest_OnAnUnconfiguredServer_Is503NotConfigured_AndNoModelCall()
    {
        using var host = new ReviewHost(settings: new(Unconfigured));
        var id = await host.ReadyForReviewAsync();

        var response = await host.RequestReviewAsync(id);

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "review-not-configured");
        Assert.Equal(0, host.ReviewClient.CallCount);
        await ReviewHttp.AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task InvalidModelSettings_AlsoGive503NotConfigured()
    {
        using var host = new ReviewHost(toolsOn: false, modelConfigured: false);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var response = await host.RequestReviewAsync(id);

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "review-not-configured");
        Assert.Equal(0, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task OnAConfiguredServer_AnIneligibleRequest_IsAConflict_DecidedByTheReviewCapability()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync(); // exists, not Approved

        var response = await host.RequestReviewAsync(id);

        await ReviewHttp.AssertErrorAsync(response, HttpStatusCode.Conflict, "conflict");
        Assert.Equal(0, host.ReviewClient.CallCount);
    }

    [Fact]
    public async Task TheNotConfiguredAnswer_NamesNoSettingAndNoValue()
    {
        using var host = new ReviewHost(settings: new(Unconfigured));
        var id = await host.ReadyForReviewAsync();

        var response = await host.RequestReviewAsync(id);

        var text = await response.Content.ReadAsStringAsync() + string.Join("\n", host.Logs.Lines);
        Assert.DoesNotContain(ReviewSettings.MaxFindingsVariable, text);
        Assert.DoesNotContain("lots-of-garbage", text);
        Assert.DoesNotContain(ReviewHost.Endpoint, text);
        Assert.DoesNotContain(ReviewHost.Deployment, text);
    }
}
