using System.Net;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US2, FR-011: with the switch off (the default) nothing about the design operations changes.
/// The full recorded responses are held by the existing golden transcripts, which must pass
/// unchanged; these tests add the tools-specific facts.
/// </summary>
public sealed class ToolsOffUnchangedTests
{
    private static readonly string[] GarbageToolSettings =
    {
        "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS", "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_MODEL_CALLS",
        "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_RETURNED_BYTES", "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_FILE_BYTES",
        "AGENTICCHANGEOPS_SAMPLE_TOOLS_TIME_LIMIT_SECONDS"
    };

    [Fact]
    public async Task ADesign_HasNoFileList_NoToolEntries_AndTheUsualAudit()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsSyntheticContent());
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json.TryGetProperty("sampleFilesRead", out _));
        Assert.Equal(new[] { "Submitted", "Approved", "DesignGenerated" }, await host.AuditEventTypesAsync(id));
        Assert.Equal(1, host.Generator.CallCount);
    }

    [Fact]
    public async Task TheHistoryAndLatestRoutes_NeverShowTheFileListMember_WhenToolsAreOff()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsSyntheticContent());
        var id = await host.ApprovedWithBasisAsync();
        await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        var latest = await (await host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();

        Assert.False(latest.TryGetProperty("sampleFilesRead", out _));
        Assert.False(history[0].TryGetProperty("sampleFilesRead", out _));
        var audit = (await host.Client.GetStringAsync($"/change-requests/{id}/audit"));
        foreach (var name in new[] { "toolName", "toolTarget", "toolOutcome", "resultBytes", "elapsedMilliseconds" })
            Assert.DoesNotContain(name, audit);
    }

    [Fact]
    public async Task WithTheSwitchOff_EveryOtherToolsSettingIsIgnored_EvenWhenGarbage()
    {
        var settings = GarbageToolSettings.ToDictionary(k => k, _ => (string?)"garbage");
        settings["AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED"] = "false";
        settings["AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH"] = "\0not-a-path";
        using var host = new SampleToolsHost(settings, toolsOn: false);

        var availability = host.Services.GetRequiredService<DesignAvailability>();

        Assert.True(availability.IsConfigured);
        Assert.DoesNotContain(host.Logs.Lines, l => l.Contains("garbage") || l.Contains("SAMPLE_TOOLS"));
        var id = await host.ApprovedAsync();
        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, host.ToolClient.Calls);
    }

    [Fact]
    public async Task WithNoToolsSettingAtAll_TheToolClientIsNeverUsed()
    {
        using var host = new SampleToolsHost(toolsOn: false);
        var id = await host.ApprovedAsync();

        await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(0, host.ToolClient.Calls);
        Assert.DoesNotContain("ToolCalled", (await host.AuditAsync(id)).GetRawText());
    }
}
