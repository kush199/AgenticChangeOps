using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

public class RequirementsBasisAdapterTests
{
    private static RequirementsBasisAdapter AdapterFor(ApiFactory factory) =>
        new(factory.Services.GetRequiredService<IRequirementsDraftingService>());

    [Fact]
    public async Task GetLatest_ForARequestWithNoRequirementsDraft_IsNull()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        var basis = AdapterFor(factory).GetLatest(id);

        Assert.Null(basis);
    }

    [Fact]
    public void GetLatest_ForAnUnknownRequest_IsNull()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);

        var basis = AdapterFor(factory).GetLatest(Guid.NewGuid());

        Assert.Null(basis);
    }

    [Fact]
    public async Task GetLatest_ReturnsTheHighestVersionWithItsSections()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();
        (await client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        var stored = factory.Services.GetRequiredService<IRequirementsDraftingService>()
            .GetLatestDraft(id).Value!;

        var basis = AdapterFor(factory).GetLatest(id);

        Assert.NotNull(basis);
        Assert.Equal(2, basis!.Version);
        Assert.Equal(stored.Summary, basis.Summary);
        Assert.Equal(stored.FunctionalRequirements, basis.FunctionalRequirements);
        Assert.Equal(stored.AcceptanceCriteria, basis.AcceptanceCriteria);
        Assert.Equal(stored.Assumptions, basis.Assumptions);
        Assert.Equal(stored.OpenQuestions, basis.OpenQuestions);
    }
}
