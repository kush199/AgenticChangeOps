using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// What a caller sends is never echoed back in an error or written to a log, and nothing about the
/// model or its settings reaches a decision response.
/// </summary>
public sealed class DesignDecisionNoLeakTests : IDisposable
{
    private const string SentinelDecision = "decision-sentinel-71c2";
    private const string SentinelName = "name-sentinel-9d41";
    private const string SentinelReason = "reason-sentinel-3b7f";

    private readonly DesignHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<string[]> AllResponsesAsync(Guid id)
    {
        var responses = new List<HttpResponseMessage>
        {
            await _host.DecideAsync(id, 1, SentinelDecision, SentinelName, SentinelReason),
            await _host.DecideAsync(id, 1, "Reject", SentinelName, null),
            await _host.DecideAsync(id, 9, "Accept", SentinelName, SentinelReason),
            await _host.DecideAsync(id, "abc", DesignDecisionHttp.Body("Accept", SentinelName, SentinelReason)),
            await _host.DecideAsync(id, 1, "Accept", new string('n', 101) + SentinelName, SentinelReason),
            await _host.DecideAsync(Guid.NewGuid(), 1, "Accept", SentinelName, SentinelReason),
            await _host.DecideAsync(id, 1, "Accept", SentinelName, SentinelReason)
        };
        responses.Add(await _host.DecideAsync(id, 1, "Accept", SentinelName, SentinelReason));
        var texts = new List<string>();
        foreach (var response in responses)
            texts.Add(await response.Content.ReadAsStringAsync());
        return texts.ToArray();
    }

    [Fact]
    public async Task NoErrorBody_EchoesTheDecisionValue()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var texts = await AllResponsesAsync(id);

        Assert.DoesNotContain(texts.Take(1), t => t.Contains(SentinelDecision) || t.Contains(SentinelName) || t.Contains(SentinelReason));
        Assert.DoesNotContain(texts.Skip(1).Take(6), t => t.Contains(SentinelDecision));
    }

    [Fact]
    public async Task RefusalBodies_CarryNoNameOrReasonOfTheCaller()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        (await _host.DecideAsync(id, 1, "Accept", "Jordan Reed")).EnsureSuccessStatusCode();

        foreach (var response in new[]
                 {
                     await _host.DecideAsync(id, 1, "Accept", SentinelName, SentinelReason),
                     await _host.DecideAsync(id, 7, "Accept", SentinelName, SentinelReason),
                     await _host.DecideAsync(Guid.NewGuid(), 1, "Accept", SentinelName, SentinelReason)
                 })
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(SentinelName, text);
            Assert.DoesNotContain(SentinelReason, text);
        }
    }

    [Fact]
    public async Task NothingTheCallerSent_IsWrittenToALog()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        await AllResponsesAsync(id);

        Assert.DoesNotContain(_host.Logs.Lines, l =>
            l.Contains(SentinelDecision) || l.Contains(SentinelName) || l.Contains(SentinelReason));
    }

    [Fact]
    public async Task ASuccessfulDecision_AndTheDesignReads_CarryNoModelOrEndpointText()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var created = await (await _host.DecideAsync(id, 1, "Accept")).Content.ReadAsStringAsync();
        var latest = await (await _host.Client.GetAsync($"/change-requests/{id}/designs/latest")).Content.ReadAsStringAsync();

        foreach (var text in new[] { created, latest })
        {
            Assert.DoesNotContain("https://", text);
            Assert.DoesNotContain("deployment", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("api-key", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bearer", text);
        }
        Assert.DoesNotContain(_host.Logs.Lines, l => l.Contains("https://") || l.Contains("api-key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ASuccessfulDecision_WritesNoLogLineAtWarningOrAbove()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var baseline = _host.Logs.LevelledLines.Count;

        (await _host.DecideAsync(id, 1, "Accept", SentinelName, SentinelReason)).EnsureSuccessStatusCode();

        Assert.Empty(_host.Logs.LevelledLines.Skip(baseline)
            .Where(l => l.Level >= Microsoft.Extensions.Logging.LogLevel.Warning));
    }

    [Fact]
    public async Task ARefusedDecision_WritesOnlyTheExistingCountsOnlyRefusalLine()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        (await _host.DecideAsync(id, 1, "Accept")).EnsureSuccessStatusCode();
        var baseline = _host.Logs.LevelledLines.Count;

        await _host.DecideAsync(id, 1, "Reject", SentinelName, SentinelReason);
        await _host.DecideAsync(id, 9, "Accept", SentinelName, SentinelReason);
        await _host.DecideAsync(id, 1, SentinelDecision, SentinelName, SentinelReason);
        await _host.DecideAsync(Guid.NewGuid(), 1, "Accept", SentinelName, SentinelReason);

        var loud = _host.Logs.LevelledLines.Skip(baseline)
            .Where(l => l.Level >= Microsoft.Extensions.Logging.LogLevel.Warning)
            .Select(l => l.Line)
            .ToArray();
        Assert.Equal(4, loud.Length);
        Assert.All(loud, line => Assert.Matches(
            @"JsonErrorMiddleware: Request refused: POST /change-requests/\{id\}/designs/\{version\}/decisions -> (400|404|409) [a-z-]+\.$",
            line));
    }
}
