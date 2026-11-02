using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public sealed class DesignDecisionConcurrencyEndpointTests
{
    private const int Callers = 10;

    [Fact]
    public async Task OverlappingDecisions_OnOneVersion_GiveOne201_AndTheRest409AlreadyDecided()
    {
        using var host = new DesignHost();
        var id = await host.ApprovedWithDesignsAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, Callers).Select(i =>
            host.DecideAsync(id, 1, i % 2 == 0 ? "Accept" : "Reject", name: "Approver " + i, reason: "Because.")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var refused = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToArray();
        Assert.Equal(Callers - 1, refused.Length);
        foreach (var response in refused)
            Assert.Equal("design-already-decided", await response.ErrorCodeAsync());
        Assert.Equal(1, (await host.AuditEventTypesAsync(id)).Count(e => e == "DesignDecided"));
        var history = (await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync())
            .EnumerateArray().ToArray();
        Assert.Single(history);
        Assert.True(history[0].TryGetProperty("decision", out _));
    }

    [Fact]
    public async Task ReadsDuringTheRace_NeverShowMoreThanOneDecisionForAVersion()
    {
        using var host = new DesignHost();
        var id = await host.ApprovedWithDesignsAsync();
        using var stop = new CancellationTokenSource();

        var reader = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var latest = await host.Client.GetAsync($"/change-requests/{id}/designs/latest");
                Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
                var json = await latest.ReadJsonAsync();
                Assert.True(!json.TryGetProperty("decision", out var d) || d.ValueKind == System.Text.Json.JsonValueKind.Object);
            }
        });

        await Task.WhenAll(Enumerable.Range(0, Callers).Select(_ => host.DecideAsync(id, 1, "Accept")));
        stop.Cancel();
        await reader;

        Assert.Equal(1, (await host.AuditEventTypesAsync(id)).Count(e => e == "DesignDecided"));
    }

    [Fact]
    public async Task TheHistoryRead_DuringTheRace_ShowsOneDecisionAtMost_AndNeverChangesIt()
    {
        using var host = new DesignHost();
        var id = await host.ApprovedWithDesignsAsync(designs: 2);
        using var stop = new CancellationTokenSource();
        string? seen = null;

        var reader = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var history = (await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync())
                    .EnumerateArray().ToArray();
                Assert.Equal(2, history.Length);
                Assert.False(history[0].TryGetProperty("decision", out _));
                if (history[1].TryGetProperty("decision", out var decision))
                {
                    seen ??= decision.GetRawText();
                    Assert.Equal(seen, decision.GetRawText());
                }
            }
        });

        await Task.WhenAll(Enumerable.Range(0, Callers).Select(i =>
            host.DecideAsync(id, 2, i % 2 == 0 ? "Accept" : "Reject", name: "Approver " + i, reason: "Because.")));
        await Task.Delay(200);
        stop.Cancel();
        await reader;

        var final = (await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync())
            .EnumerateArray().Last().GetProperty("decision").GetRawText();
        if (seen is not null)
            Assert.Equal(seen, final);
    }
}
