using System.Text.Json;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>
/// Calls for the proposal-requires-an-accepted-design tests, on top of <see cref="ProposalHost"/>
/// running the real chain (no fake basis): requirements, approval, design, decision, proposal.
/// </summary>
public static class ProposalDecisionHttp
{
    public const string DesignJson = """
        {
          "approachSummary": "Change the page size setting only.",
          "affectedComponents": [ "Settings" ],
          "impactAndRisks": [ "Longer pages load more rows." ],
          "dependencies": [],
          "rollbackConsiderations": [ "Restore the previous value." ],
          "openQuestions": []
        }
        """;

    /// <summary>Submit, draft requirements, approve and draft the first design. The design is left undecided.</summary>
    public static async Task<Guid> RealChainWithUndecidedDesignAsync(this ProposalHost host, string system = "Orders Service")
    {
        var id = await host.SubmittedAsync(system);
        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        await host.DraftDesignAsync(id);
        return id;
    }

    /// <summary>Drafts another design version with the scripted design answer, then restores the proposal script.</summary>
    public static async Task DraftDesignAsync(this ProposalHost host, Guid id)
    {
        host.ToolClient.Behavior = ProposalHost.Returns(DesignJson);
        (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();
        host.ToolClient.Behavior = ProposalHost.ReadsAndProposes();
    }

    public static Task<HttpResponseMessage> DecideDesignAsync(
        this ProposalHost host, Guid id, int version, string decision, string? reason = null) =>
        host.Client.PostJsonAsync($"/change-requests/{id}/designs/{version}/decisions",
            new { approverName = "Sam Sample", decision, reason });

    public static async Task AcceptDesignAsync(this ProposalHost host, Guid id, int version) =>
        (await host.DecideDesignAsync(id, version, "Accept")).EnsureSuccessStatusCode();

    public static async Task RejectDesignAsync(this ProposalHost host, Guid id, int version) =>
        (await host.DecideDesignAsync(id, version, "Reject", "Not the approach we want.")).EnsureSuccessStatusCode();

    public static async Task<JsonElement> ProposalsAsync(this ProposalHost host, Guid id) =>
        await (await host.Client.GetAsync($"/change-requests/{id}/proposals")).ReadJsonAsync();

    public static async Task<JsonElement> WorkflowAsync(this ProposalHost host, Guid id) =>
        await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();

    public static string[] AuditTypes(JsonElement audit) =>
        audit.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray();

    public static string[] ErrorFields(JsonElement body) =>
        body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()!).ToArray();
}
