using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US5, FR-021: with the Developer unused (the default, tools off) nothing existing changes. The
/// recorded design and audit responses are held by the existing golden transcripts, which must pass
/// unchanged; these tests add the facts that no golden covers: the workflow status body at
/// DesignComplete has exactly its current members, and the new routes answer without side effects.
/// </summary>
public sealed class ProposalOffMatchesBaselineTests
{
    private static readonly string[] ExpectedStatusMembers =
    {
        "automatic", "changeRequestId", "changeRequestStatus", "stage", "completedSteps", "nextStep", "reason"
    };

    /// <summary>Submitted, given a real requirements draft, then approved; the design uses the fake basis.</summary>
    private static async Task<Guid> ApprovedWithDraftAsync(DesignHost host)
    {
        var id = await host.Client.SubmitAsync();
        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        host.Basis.Set(id, FakeBasisSource.Basis());
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task TheWorkflowStatus_AtDesignComplete_HasExactlyTheMembersItAlwaysHad()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsSyntheticContent());
        var id = await ApprovedWithDraftAsync(host);
        (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();

        var response = await host.Client.GetAsync($"/change-requests/{id}/workflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.ReadJsonAsync();
        Assert.Equal("DesignComplete", status.GetProperty("stage").GetString());
        Assert.Equal(
            ExpectedStatusMembers.OrderBy(n => n),
            status.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.DoesNotContain("offeredStep", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheAdvanceResponse_AtDesignComplete_IsUnchanged()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsSyntheticContent());
        var id = await ApprovedWithDraftAsync(host);
        (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();

        var response = await host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("no-step-due", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheDesignAudit_IsUnchanged_AndTheNewRoutesAddNothingToIt()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsSyntheticContent());
        var id = await ApprovedWithDraftAsync(host);
        (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();

        var post = await host.Client.PostAsync($"/change-requests/{id}/proposals", null);
        var latest = await host.Client.GetAsync($"/change-requests/{id}/proposals/latest");
        var history = await host.Client.GetAsync($"/change-requests/{id}/proposals");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, post.StatusCode);
        Assert.Equal("proposal-tools-unavailable", (await post.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, latest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(new[] { "Submitted", "DraftGenerated", "Approved", "DesignGenerated" }, await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task TheDesignResponse_HasNoProposalMembers()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsSyntheticContent());
        var id = await ApprovedWithDraftAsync(host);

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("proposal", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verificationNote", text);
    }
}
