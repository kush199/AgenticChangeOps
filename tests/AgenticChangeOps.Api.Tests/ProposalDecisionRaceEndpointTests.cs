using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US5 smoke test (the deterministic proof is the service-level stale-at-store test): a proposal
/// request racing a new design request never stores a proposal on a design that is not the
/// accepted latest one.
/// </summary>
public sealed class ProposalDecisionRaceEndpointTests
{
    [Fact]
    public async Task AProposalRacingANewDesign_IsEitherStoredOnTheAcceptedLatest_OrRefused()
    {
        for (var round = 0; round < 8; round++)
        {
            using var host = new ProposalHost(useFakeBasis: false);
            var id = await host.RealChainWithUndecidedDesignAsync();
            await host.AcceptDesignAsync(id, 1);
            var first = await (await host.ProposeAsync(id)).ReadJsonAsync();

            host.ToolClient.Behavior = ProposalHost.ReadsAndProposes();
            var propose = host.ProposeAsync(id);
            var redesign = host.Client.PostAsync($"/change-requests/{id}/designs", null);
            var responses = await Task.WhenAll(propose, redesign);

            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
            var proposals = (await host.ProposalsAsync(id)).EnumerateArray().ToArray();

            // The earlier proposal is untouched, and any later one is on the accepted version 1 only:
            // version 2, if it exists, was never decided.
            Assert.Equal(first.GetProperty("proposalId").GetString(), proposals[0].GetProperty("proposalId").GetString());
            foreach (var proposal in proposals)
                Assert.Equal(1, proposal.GetProperty("designVersion").GetInt32());
        }
    }
}
