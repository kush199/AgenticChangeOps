using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US3 over HTTP: every refusal by the review capability is a conflict naming the rule, comes before
/// any model call and stores nothing. The endpoint adds no rule of its own: the named rules are exactly
/// what the service reports.
/// </summary>
public sealed class ReviewRefusalEndpointTests
{
    private static async Task AssertConflictAsync(ReviewHost host, Guid id, HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("conflict", body.GetProperty("code").GetString());
        Assert.Equal(new[] { field }, body.FieldNames());
        Assert.False(body.TryGetProperty("category", out _));
        Assert.Equal(0, host.ReviewClient.CallCount);
        await ReviewHttp.AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task NotApproved_IsAConflict_OnStatus()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();

        await AssertConflictAsync(host, id, await host.RequestReviewAsync(id), "Status");
    }

    [Fact]
    public async Task NoRequirementsDraft_IsAConflict_OnRequirementsDraft()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();

        await AssertConflictAsync(host, id, await host.RequestReviewAsync(id), "RequirementsDraft");
    }

    [Fact]
    public async Task NoDesignDraft_IsAConflict_OnDesignDraft()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();
        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();

        await AssertConflictAsync(host, id, await host.RequestReviewAsync(id), "DesignDraft");
    }

    [Fact]
    public async Task ADesignOnOlderRequirements_IsAConflict_OnDesignDraftVersion()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.BasisShaper = b => b with { Design = b.Design! with { RequirementsVersion = b.Requirements!.Version + 1 } };

        await AssertConflictAsync(host, id, await host.RequestReviewAsync(id), "DesignDraftVersion");
    }

    [Fact]
    public async Task NoProposal_IsAConflict_OnProposal()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForProposalAsync();

        await AssertConflictAsync(host, id, await host.RequestReviewAsync(id), "Proposal");
    }

    [Fact]
    public async Task AProposalOnAnOlderDesign_IsAConflict_OnProposalVersion()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.BasisShaper = b => b with { Proposal = b.Proposal! with { DesignVersion = b.Design!.Version + 1 } };

        await AssertConflictAsync(host, id, await host.RequestReviewAsync(id), "ProposalVersion");
    }

    [Fact]
    public async Task ARecordThatChangesWhileTheModelRuns_IsAConflict_OnProposalVersion_AndStoresNothing()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        host.ReviewClient.Behavior = ct =>
        {
            // A newer proposal appears while the model is still working.
            host.BasisShaper = b => b with { Proposal = b.Proposal! with { Version = b.Proposal.Version + 1 } };
            return ScriptedChatModelClient.ReturnsText(ReviewHost.ValidReviewJson)(ct);
        };

        var response = await host.RequestReviewAsync(id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("conflict", body.GetProperty("code").GetString());
        Assert.Equal(new[] { "ProposalVersion" }, body.FieldNames());
        Assert.Equal(1, host.ReviewClient.CallCount);
        await ReviewHttp.AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task TheReportedFields_AreExactlyThoseTheServiceReports()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();

        var viaHttp = (await (await host.RequestReviewAsync(id)).ReadJsonAsync()).FieldNames();
        var viaService = (await host.Reviewing.ReviewAsync(id)).Errors.Select(e => e.FieldName).ToArray();

        Assert.Equal(viaService, viaHttp);
    }
}
