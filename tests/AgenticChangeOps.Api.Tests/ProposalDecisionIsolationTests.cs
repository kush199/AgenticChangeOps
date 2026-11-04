using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US6: a refused proposal request changes nothing: not the workflow status, not the change
/// request, not the design decisions, not the audit trail.
/// </summary>
public sealed class ProposalDecisionIsolationTests
{
    [Fact]
    public async Task ARefusedProposal_LeavesStatusWorkflowDecisionsAndAuditExactlyAsTheyWere()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();
        var workflowBefore = (await host.WorkflowAsync(id)).ToString();
        var auditBefore = (await host.AuditAsync(id)).ToString();
        var statusBefore = await host.StatusAsync(id);

        Assert.Equal(HttpStatusCode.Conflict, (await host.ProposeAsync(id)).StatusCode);

        Assert.Equal(workflowBefore, (await host.WorkflowAsync(id)).ToString());
        Assert.Equal(auditBefore, (await host.AuditAsync(id)).ToString());
        Assert.Equal(statusBefore, await host.StatusAsync(id));
        Assert.DoesNotContain("DesignDecided", ProposalDecisionHttp.AuditTypes(await host.AuditAsync(id)));
    }

    [Fact]
    public async Task ARefusedProposal_NeverRecordsOrChangesAnyDecision()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();
        await host.RejectDesignAsync(id, 1);
        var designBefore = await (await host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await host.ProposeAsync(id)).StatusCode);

        var designAfter = await (await host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        Assert.Equal(designBefore.ToString(), designAfter.ToString());
        Assert.Equal("Rejected", designAfter.GetProperty("decision").GetProperty("outcome").GetString());
        Assert.Single(ProposalDecisionHttp.AuditTypes(await host.AuditAsync(id)), t => t == "DesignDecided");
    }
}
