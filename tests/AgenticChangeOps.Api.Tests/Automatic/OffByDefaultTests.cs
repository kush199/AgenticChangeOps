using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Automatic;

public class OffByDefaultTests
{
    private static string Status(Guid id) => $"/change-requests/{id}/workflow";

    [Fact]
    public async Task WithNoAutomaticSettings_NothingRuns_AndTheStatusSaysOff()
    {
        var generator = FakeDraftGenerator.ReturnsSyntheticContent();
        using var factory = new ApiFactory(generator, draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        await Task.Delay(500);

        Assert.Equal(0, generator.CallCount);
        Assert.Null(factory.Services.GetRequiredService<CheckSummaryHolder>().Latest);
        var status = await (await client.GetAsync(Status(id))).ReadJsonAsync();
        Assert.Equal("Off", status.GetProperty("automatic").GetProperty("mode").GetString());
        Assert.Equal(0, status.GetProperty("completedSteps").GetArrayLength());
    }

    [Fact]
    public async Task TheOperatorEndpoint_ReportsDisabled_AndNoCheckYet()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/operator/automatic-advancement");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("lastCheck").ValueKind);
    }

    [Fact]
    public async Task AnInvalidEnabledValue_StillStartsTheHost_AndLeavesTheFeatureOff()
    {
        var generator = FakeDraftGenerator.ReturnsSyntheticContent();
        using var factory = new ApiFactory(
            generator, draftingConfigured: true,
            settings: new Dictionary<string, string?> { ["ChangeOps:AutoAdvance:Enabled"] = "sometimes" });
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        await Task.Delay(300);

        Assert.Equal(0, generator.CallCount);
        var status = await (await client.GetAsync(Status(id))).ReadJsonAsync();
        Assert.Equal("Off", status.GetProperty("automatic").GetProperty("mode").GetString());
        Assert.Contains(factory.Logs.Lines, l => l.Contains("ChangeOps:AutoAdvance:Enabled") && l.Contains("invalid"));
        Assert.DoesNotContain(factory.Logs.Lines, l => l.Contains("sometimes"));
    }

    [Fact]
    public async Task AManualAdvance_StillWorks_AndItsAuditEntryHasNoAutomaticMarker()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        (await client.PostAsync($"/change-requests/{id}/workflow/advance", null)).EnsureSuccessStatusCode();

        var audit = await (await client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        Assert.All(audit.EnumerateArray(), e => Assert.False(e.TryGetProperty("automatic", out _)));
    }
}
