using System.Text.Json;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Automatic advancement is unchanged by a decision: it neither records one nor reacts to one. A
/// rejected design is not redrawn on its own, and an accepted one starts nothing.
/// </summary>
public sealed class DesignDecisionAutomaticAdvancementTests
{
    private static readonly Dictionary<string, string?> FastOn = new()
    {
        ["ChangeOps:AutoAdvance:Enabled"] = "true",
        ["ChangeOps:AutoAdvance:IntervalSeconds"] = "1"
    };

    private static async Task<T?> PollAsync<T>(Func<Task<T?>> read, Func<T?, bool> done, int seconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        T? value = default;
        while (DateTime.UtcNow < deadline)
        {
            value = await read();
            if (done(value))
                return value;
            await Task.Delay(100);
        }
        return value;
    }

    private static async Task<string> StepsAsync(HttpClient client, Guid id)
    {
        var status = await (await client.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();
        return string.Join("|", new[] { "stage", "nextStep", "reason", "completedSteps" }
            .Select(name => status.TryGetProperty(name, out var value) ? value.GetRawText() : "absent"));
    }

    [Theory]
    [InlineData("Accept", null)]
    [InlineData("Reject", "Needs a rollback plan.")]
    public async Task ADecidedDesign_IsLeftAlone_ByTheAutomaticPasses(string decision, string? reason)
    {
        var designs = FakeDesignGenerator.ReturnsSyntheticContent();
        using var factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(),
            draftingConfigured: true,
            settings: FastOn,
            configureServices: services =>
            {
                services.AddSingleton<IDesignDraftGenerator>(designs);
                services.AddSingleton(new DesignAvailability(true));
            });
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();
        (await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();

        // The automatic passes draft the requirements and then the design, unasked.
        var drafted = await PollAsync(
            async () => await (await client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync(),
            history => history.ValueKind == JsonValueKind.Array && history.GetArrayLength() == 1);
        Assert.Equal(1, drafted.GetArrayLength());
        await Task.Delay(1500); // let any pending pass finish before comparing
        var stepsBefore = await StepsAsync(client, id);
        var callsBefore = designs.CallCount;

        var response = await client.PostJsonAsync(
            $"/change-requests/{id}/designs/1/decisions",
            new { approverName = "Jordan Reed", decision, reason });
        response.EnsureSuccessStatusCode();
        await Task.Delay(3500); // several automatic passes at a one-second interval

        Assert.Equal(callsBefore, designs.CallCount);
        Assert.Equal(1, (await (await client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync()).GetArrayLength());
        Assert.Equal(stepsBefore, await StepsAsync(client, id));
        var audit = await (await client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        var decided = audit.EnumerateArray().Where(e => e.GetProperty("eventType").GetString() == "DesignDecided").ToArray();
        var entry = Assert.Single(decided);
        Assert.False(entry.TryGetProperty("automatic", out _));
        Assert.Equal("Jordan Reed", entry.GetProperty("approverName").GetString());
    }

    [Fact]
    public async Task WithAutomaticAdvancementOn_NoDecisionIsEverRecordedUnasked()
    {
        using var factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(),
            draftingConfigured: true,
            settings: FastOn,
            configureServices: services =>
            {
                services.AddSingleton<IDesignDraftGenerator>(FakeDesignGenerator.ReturnsSyntheticContent());
                services.AddSingleton(new DesignAvailability(true));
            });
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();
        (await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        await PollAsync(
            async () => await (await client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync(),
            history => history.ValueKind == JsonValueKind.Array && history.GetArrayLength() == 1);

        await Task.Delay(3500);

        var audit = await (await client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        Assert.DoesNotContain(audit.EnumerateArray(), e => e.GetProperty("eventType").GetString() == "DesignDecided");
        var latest = await (await client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        Assert.False(latest.TryGetProperty("decision", out _));
    }
}
