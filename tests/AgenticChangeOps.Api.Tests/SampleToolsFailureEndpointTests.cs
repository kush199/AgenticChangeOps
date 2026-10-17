using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US4: the two new failure categories over HTTP, and the check order that must not change.</summary>
public sealed class SampleToolsFailureEndpointTests
{
    private static async Task AssertNothingStoredAsync(SampleToolsHost host, Guid id)
    {
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
        Assert.Equal(new[] { "Submitted", "Approved" },
            (await host.AuditAsync(id)).EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray());
        var request = await (await host.Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Approved", request.GetProperty("status").GetString());
    }

    private static void AssertNoLeaks(SampleToolsHost host, string responseText)
    {
        var haystack = responseText + "\n" + string.Join("\n", host.Logs.Lines);
        foreach (var forbidden in new[]
                 {
                     SampleToolsHost.Endpoint, "sentinel-endpoint", SampleToolsHost.Deployment,
                     "orders.page.size", "acceptOrder", SampleToolsHost.RepositoryRoot()
                 })
            Assert.DoesNotContain(forbidden, haystack);
    }

    [Fact]
    public async Task ASampleFolderThatDoesNotExist_Returns502_ToolsUnavailable_AndStoresNothing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "acops-missing-" + Guid.NewGuid().ToString("N"));
        using var host = new SampleToolsHost(new Dictionary<string, string?> { ["AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH"] = missing });
        var id = await host.ApprovedAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(text).RootElement;
        Assert.Equal("generation-failed", json.GetProperty("code").GetString());
        Assert.Equal("ToolsUnavailable", json.GetProperty("category").GetString());
        Assert.Equal(new[] { "category", "code", "message" }, json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(0, host.ToolClient.Calls);
        Assert.DoesNotContain(missing, text);
        await AssertNothingStoredAsync(host, id);
        AssertNoLeaks(host, text);
    }

    [Fact]
    public async Task LimitsExhausted_Returns502_WithItsOwnCategory_AndStoresNothing()
    {
        using var host = new SampleToolsHost(behavior: async (session, ct) =>
        {
            await session.Tools.InvokeAsync("read_file", "{\"path\":\"orders-service/src/OrderIntake\"}", ct);
            throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);
        });
        var id = await host.ApprovedAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal("LimitsExhausted",
            System.Text.Json.JsonDocument.Parse(text).RootElement.GetProperty("category").GetString());
        await AssertNothingStoredAsync(host, id);
        AssertNoLeaks(host, text);
    }

    [Theory]
    [InlineData("AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS", "0")]
    [InlineData("AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_MODEL_CALLS", "-2")]
    [InlineData("AGENTICCHANGEOPS_SAMPLE_TOOLS_TIME_LIMIT_SECONDS", "soon")]
    [InlineData("AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED", "maybe")]
    public async Task AnInvalidToolsSetting_MakesDesignNotConfigured_NamingNoValue(string setting, string value)
    {
        using var host = new SampleToolsHost(new Dictionary<string, string?> { [setting] = value });
        var id = await host.ApprovedAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("design-not-configured", text);
        Assert.DoesNotContain(value, text);
        Assert.DoesNotContain(host.Logs.Lines, l => l.Contains($"\"{value}\"") || l.EndsWith(value));
        Assert.Equal(0, host.ToolClient.Calls);
    }

    [Fact]
    public async Task TheOrderOfChecks_IsUnchanged_EvenWhenToolsAreMisconfigured()
    {
        using var host = new SampleToolsHost(new Dictionary<string, string?> { ["AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS"] = "0" });

        var malformed = await host.Client.PostAsync("/change-requests/not-a-guid/designs", null);
        var unknown = await host.Client.PostAsync($"/change-requests/{Guid.NewGuid()}/designs", null);

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.ToolsUnavailable)]
    [InlineData(GenerationFailureCategory.LimitsExhausted)]
    public async Task AutomaticAdvancement_RecordsTheNewCategories_LikeAnyOther(GenerationFailureCategory category)
    {
        using var host = new SampleToolsHost(
            new Dictionary<string, string?>
            {
                ["ChangeOps:AutoAdvance:Enabled"] = "true",
                ["ChangeOps:AutoAdvance:IntervalSeconds"] = "3600"
            },
            behavior: (_, _) => throw new DraftGenerationException(category));
        var id = await host.ApprovedAsync();

        // The requirements draft is due first (the workflow reads the real store), then the design.
        var service = host.Services.GetRequiredService<AutomaticAdvancementService>();
        var failures = 0;
        for (var check = 0; check < 3 && failures == 0; check++)
            failures += (await service.RunCheckAsync()).FailuresByCategory.GetValueOrDefault(category);

        Assert.Equal(1, failures);
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
    }
}
