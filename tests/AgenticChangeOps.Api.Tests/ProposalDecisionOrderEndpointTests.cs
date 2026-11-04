using System.Net;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US4 and US6 end to end through the real chain: only the latest design counts, and every
/// existing refusal keeps its reason and its place in the order.
/// </summary>
public sealed class ProposalDecisionOrderEndpointTests
{
    [Fact]
    public async Task OnlyTheLatestDesignsDecisionCounts_AcrossNewVersions()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await host.ProposeAsync(id)).StatusCode);

        await host.AcceptDesignAsync(id, 1);
        Assert.Equal(HttpStatusCode.Created, (await host.ProposeAsync(id)).StatusCode);

        await host.DraftDesignAsync(id);
        var undecidedAgain = await host.ProposeAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, undecidedAgain.StatusCode);
        Assert.Contains("not been decided",
            (await undecidedAgain.ReadJsonAsync()).GetProperty("errors")[0].GetProperty("message").GetString());

        await host.RejectDesignAsync(id, 2);
        var rejected = await host.ProposeAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("was rejected",
            (await rejected.ReadJsonAsync()).GetProperty("errors")[0].GetProperty("message").GetString());

        await host.DraftDesignAsync(id);
        await host.AcceptDesignAsync(id, 3);
        var accepted = await host.ProposeAsync(id);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(3, (await accepted.ReadJsonAsync()).GetProperty("designVersion").GetInt32());

        // The first proposal, made on version 1, was never altered or removed.
        var history = await host.ProposalsAsync(id);
        Assert.Equal(new[] { 1, 3 }, history.EnumerateArray().Select(p => p.GetProperty("designVersion").GetInt32()));
    }

    [Fact]
    public async Task EveryExistingRefusal_IsUnchanged_EvenWhenTheDesignIsUndecided()
    {
        using var host = new ProposalHost();

        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsync("/change-requests/not-a-guid/proposals", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.ProposeAsync(Guid.NewGuid())).StatusCode);

        var notApproved = await host.SubmittedAsync();
        host.Basis.Set(notApproved, FakeProposalBasis.Current(decision: ProposalDesignVerdict.None));
        var status = await host.ProposeAsync(notApproved);
        Assert.Equal(HttpStatusCode.Conflict, status.StatusCode);
        Assert.Equal(new[] { "Status" }, ProposalDecisionHttp.ErrorFields(await status.ReadJsonAsync()));

        var noRequirements = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(noRequirements, new ProposalBasis(null, null));
        Assert.Equal(new[] { "RequirementsDraft" },
            ProposalDecisionHttp.ErrorFields(await (await host.ProposeAsync(noRequirements)).ReadJsonAsync()));

        var noDesign = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(noDesign, new ProposalBasis(FakeProposalBasis.Current().Requirements, null));
        Assert.Equal(new[] { "DesignDraft" },
            ProposalDecisionHttp.ErrorFields(await (await host.ProposeAsync(noDesign)).ReadJsonAsync()));

        var oldRequirements = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(oldRequirements, FakeProposalBasis.Current(requirementsVersion: 1, designVersion: 1) with
        {
            Requirements = FakeProposalBasis.Current(requirementsVersion: 2).Requirements
        });
        Assert.Equal(new[] { "DesignDraftVersion" },
            ProposalDecisionHttp.ErrorFields(await (await host.ProposeAsync(oldRequirements)).ReadJsonAsync()));

        Assert.Equal(0, host.ToolClient.Calls);
    }

    [Fact]
    public async Task AvailabilityErrors_StillComeBeforeTheDecisionCheck()
    {
        using var host = new ProposalHost(toolsOn: false);
        var id = await host.ApprovedAsync();
        host.Basis.Set(id, FakeProposalBasis.Current(decision: ProposalDesignVerdict.None));

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("proposal-tools-unavailable", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }
}
