using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement;

namespace AgenticChangeOps.Api.Tests;

public class DraftFailureTests
{
    private const string Sentinel = "sentinel-secret-text-7f3a";

    private static async Task<(ApiFactory Factory, HttpClient Client, Guid Id)> StartAsync(FakeDraftGenerator generator)
    {
        var factory = new ApiFactory(generator, draftingConfigured: true);
        var client = factory.CreateClient();
        var id = await client.SubmitAsync();
        return (factory, client, id);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task GenerationFailure_Returns502CarryingOnlyTheCategory(GenerationFailureCategory category)
    {
        var (factory, client, id) = await StartAsync(FakeDraftGenerator.ThrowsCategory(category));
        using var _ = factory;
        using var __ = client;

        var response = await client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", json.GetProperty("code").GetString());
        Assert.Equal(category.ToString(), json.GetProperty("category").GetString());
        var members = json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "category", "code", "message" }, members);
    }

    [Fact]
    public async Task GenerationFailure_StoresNothing()
    {
        var (factory, client, id) = await StartAsync(
            FakeDraftGenerator.ThrowsCategory(GenerationFailureCategory.TimedOut));
        using var _ = factory;
        using var __ = client;

        await client.PostAsync($"/change-requests/{id}/drafts", null);

        var history = await (await client.GetAsync($"/change-requests/{id}/drafts")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
        var audit = await (await client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        Assert.Single(audit.EnumerateArray());
        var request = await (await client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Submitted", request.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ArbitraryGeneratorException_Returns500AndLeaksNothing()
    {
        var generator = FakeDraftGenerator.ThrowsArbitrary(new InvalidOperationException(Sentinel));
        var (factory, client, id) = await StartAsync(generator);
        using var _ = factory;
        using var __ = client;

        var response = await client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("unexpected-error", text);
        Assert.DoesNotContain(Sentinel, text);
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain(factory.Logs.Lines, line => line.Contains(Sentinel));
    }

    [Fact]
    public async Task BlankGeneratedContent_Returns502WithMalformedOutput()
    {
        var blank = new DraftContent("   ", new[] { "a" }, new[] { "b" }, new[] { "c" }, new[] { "d" });
        var (factory, client, id) = await StartAsync(FakeDraftGenerator.ReturnsContent(blank));
        using var _ = factory;
        using var __ = client;

        var response = await client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", json.GetProperty("code").GetString());
        Assert.Equal("MalformedOutput", json.GetProperty("category").GetString());
    }
}
