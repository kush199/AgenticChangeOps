using System.Net;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US3 over HTTP: every refusal is specific, comes before any model call, and stores nothing.
/// Order of checks: id (400), existence (404), availability (503), then state (409).
/// </summary>
public sealed class ProposalRefusalEndpointTests
{
    private static async Task AssertNothingStoredAsync(ProposalHost host, Guid id)
    {
        Assert.Equal(0, host.ToolClient.Calls);
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/proposals")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
        var audit = (await host.AuditAsync(id)).EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToArray();
        Assert.DoesNotContain("ProposalGenerated", audit);
        Assert.DoesNotContain("ToolCalled", audit);
    }

    // ---- availability ----

    [Fact]
    public async Task ToolsSwitchedOff_Is503ProposalToolsUnavailable_BeforeAnyModelCall()
    {
        using var host = new ProposalHost(toolsOn: false);
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("proposal-tools-unavailable", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        await AssertNothingStoredAsync(host, id);
    }

    [Theory]
    [InlineData(ProposalSettings.MaxChangesVariable, "0")]
    [InlineData(ProposalSettings.MaxChangesVariable, "-1")]
    [InlineData(ProposalSettings.MaxChangesVariable, "2.5")]
    [InlineData(ProposalSettings.MaxChangesVariable, "many")]
    [InlineData(ProposalSettings.MaxBytesVariable, "0")]
    [InlineData(ProposalSettings.MaxBytesVariable, "+5")]
    [InlineData(ProposalSettings.MaxBytesVariable, "lots")]
    public async Task InvalidProposalLimits_Is503ProposalNotConfigured_AndDesignDraftingStillWorks(string name, string value)
    {
        using var host = new ProposalHost(settings: new() { [name] = value });
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("proposal-not-configured", body.GetProperty("code").GetString());
        Assert.DoesNotContain(value, body.ToString().Replace("\"" + name + "\"", string.Empty));
        await AssertNothingStoredAsync(host, id);
        var warnings = string.Join("\n", host.Logs.Lines);
        Assert.DoesNotContain(value + "\"", warnings);
    }

    [Fact]
    public async Task ABadProposalLimit_IsIgnored_WhileTheToolsAreOff()
    {
        using var host = new ProposalHost(toolsOn: false, settings: new() { [ProposalSettings.MaxChangesVariable] = "garbage" });
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal("proposal-tools-unavailable", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ABadModelSetting_Is503ProposalNotConfigured()
    {
        using var host = new ProposalHost(settings: new() { ["AGENTICCHANGEOPS_MODEL_ENDPOINT"] = "not a url" });
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("proposal-not-configured", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        await AssertNothingStoredAsync(host, id);
    }

    // ---- state ----

    [Fact]
    public async Task NotApproved_Is409Conflict_OnTheStatusField()
    {
        using var host = new ProposalHost();
        var id = await host.SubmittedAsync();
        host.Basis.Set(id, FakeProposalBasis.Current());

        var response = await host.ProposeAsync(id);

        await AssertConflictAsync(response, "Status");
        await AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task NoRequirementsDraft_Is409Conflict_OnTheRequirementsField()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync(withDrafts: false);

        await AssertConflictAsync(await host.ProposeAsync(id), "RequirementsDraft");
        await AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task NoDesignDraft_Is409Conflict_OnTheDesignField()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(id, new ProposalBasis(FakeProposalBasis.Current().Requirements, null));

        await AssertConflictAsync(await host.ProposeAsync(id), "DesignDraft");
        await AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task ADesignOnOlderRequirements_Is409Conflict_OnADistinctField()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(id, new ProposalBasis(
            FakeProposalBasis.Current(requirementsVersion: 2).Requirements,
            FakeProposalBasis.Current(requirementsVersion: 1).Design));

        await AssertConflictAsync(await host.ProposeAsync(id), "DesignDraftVersion");
        await AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task ANewerDraft_SavedWhileTheModelRuns_Is409_AndStoresNothing()
    {
        Guid id = default;
        ProposalHost? hostRef = null;
        using var host = new ProposalHost(behavior: async (session, ct) =>
        {
            await session.Tools.InvokeAsync("read_file", $"{{\"path\":\"{ProposalHost.OrdersSettings}\"}}", ct);
            // A newer requirements draft appears while the model is still working; the design is
            // still the one based on version 1.
            hostRef!.Basis.Set(id, new ProposalBasis(
                FakeProposalBasis.Current(requirementsVersion: 2).Requirements,
                FakeProposalBasis.Current(requirementsVersion: 1).Design));
            return new ChatModelResponse(ProposalHost.Answer(ProposalHost.ChangeEdit()), false);
        });
        hostRef = host;
        id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        await AssertConflictAsync(response, "DesignDraftVersion");
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/proposals")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
        var audit = (await host.AuditAsync(id)).EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToArray();
        Assert.DoesNotContain("ProposalGenerated", audit);
        Assert.DoesNotContain("ToolCalled", audit);
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("conflict", body.GetProperty("code").GetString());
        Assert.Equal(new[] { field }, body.FieldNames());
    }
}
