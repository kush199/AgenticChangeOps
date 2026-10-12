using System.Net;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.AzureOpenAI;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

public class NotConfiguredTests
{
    [Fact(Skip = "synthetic")]
    public async Task NotConfigured_DraftReturns503_AndGeneratorIsNeverCalled()
    {
        var generator = FakeDraftGenerator.ReturnsSyntheticContent();
        using var factory = new ApiFactory(generator, draftingConfigured: false);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        var response = await client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("drafting-not-configured", json.GetProperty("code").GetString());
        Assert.Equal(0, generator.CallCount);
    }

    [Fact(Skip = "synthetic")]
    public async Task NotConfigured_EveryOtherOperationStillWorks()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: false);
        using var client = factory.CreateClient();

        var id = await client.SubmitAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/change-requests/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/change-requests/{id}/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/change-requests/{id}/clearance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/change-requests/{id}/drafts/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/change-requests/{id}/drafts")).StatusCode);
        var decision = await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve());
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
    }

    [Fact(Skip = "synthetic")]
    public async Task NotConfigured_InvalidIdentifierStillReturns400BeforeThe503()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/change-requests/not-a-guid/drafts", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-identifier", json.GetProperty("code").GetString());
    }

    [Theory(Skip = "synthetic")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("<your-resource-endpoint>", "<your-deployment-name>")]
    public async Task StartupWithMissingBlankOrPlaceholderSettings_DoesNotThrowAndDraftingIsNotConfigured(
        string? endpoint, string? deployment)
    {
        var settings = new Dictionary<string, string?>
        {
            [AzureOpenAIGeneratorOptions.EndpointVariable] = endpoint,
            [AzureOpenAIGeneratorOptions.DeploymentVariable] = deployment
        };
        using var factory = new ApiFactory(settings: settings);
        using var client = factory.CreateClient();

        var id = await client.SubmitAsync();
        var response = await client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("drafting-not-configured", text);
        Assert.DoesNotContain(AzureOpenAIGeneratorOptions.EndpointVariable, text);
        Assert.DoesNotContain(AzureOpenAIGeneratorOptions.DeploymentVariable, text);
    }

    [Fact(Skip = "synthetic")]
    public void StartupWithValidSyntheticSettings_MarksDraftingConfigured_WithoutCallingAnyModel()
    {
        var settings = new Dictionary<string, string?>
        {
            [AzureOpenAIGeneratorOptions.EndpointVariable] = "https://synthetic-models.invalid/",
            [AzureOpenAIGeneratorOptions.DeploymentVariable] = "synthetic-deployment"
        };
        using var factory = new ApiFactory(settings: settings);

        var availability = factory.Services.GetRequiredService<DraftingAvailability>();

        Assert.True(availability.IsConfigured);
    }
}
