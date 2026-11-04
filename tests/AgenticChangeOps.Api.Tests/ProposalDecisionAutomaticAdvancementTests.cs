using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// With automatic advancement switched on, a design that is undecided or Rejected is treated
/// exactly as before: the automatic path never requests a proposal for it and never records,
/// changes or implies a design decision. Only a person decides.
/// </summary>
public sealed class ProposalDecisionAutomaticAdvancementTests
{
    private static readonly Dictionary<string, string?> FastOn = new()
    {
        ["ChangeOps:AutoAdvance:Enabled"] = "true",
        ["ChangeOps:AutoAdvance:IntervalSeconds"] = "1"
    };

    private static async Task<bool> WaitAsync(Func<Task<bool>> condition, int seconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;
            await Task.Delay(150);
        }

        return false;
    }

    // With automatic advancement on, the system drafts the requirements and (after a person approves)
    // the design by itself. A person approves; the design is left undecided.
    private static async Task<Guid> AutomaticallyDraftedUndecidedDesignAsync(ProposalHost host)
    {
        host.ToolClient.Behavior = ProposalHost.Returns(ProposalDecisionHttp.DesignJson);
        var id = await host.SubmittedAsync();
        Assert.True(await WaitAsync(async () =>
            (await host.WorkflowAsync(id)).GetProperty("completedSteps").GetArrayLength() >= 1));
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        Assert.True(await WaitAsync(async () =>
            (await host.Client.GetAsync($"/change-requests/{id}/designs/latest")).IsSuccessStatusCode));
        host.ToolClient.Behavior = ProposalHost.ReadsAndProposes();
        return id;
    }

    // Lets the hosted service run at least two full checks after the setup is complete.
    private static async Task LetAutomaticPassesRunAsync(ProposalHost host)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        var seen = 0;
        while (DateTime.UtcNow < deadline && seen < 2)
        {
            var json = await (await host.Client.GetAsync("/operator/automatic-advancement")).ReadJsonAsync();
            if (json.GetProperty("lastCheck").ValueKind == JsonValueKind.Object)
                seen++;
            await Task.Delay(1100);
        }

        Assert.True(seen >= 2, "The automatic passes did not run in time.");
    }

    private static async Task AssertAutomaticPathDidNothingAsync(ProposalHost host, Guid id, int expectedDecisions)
    {
        var modelCallsBefore = host.ToolClient.Calls;
        var workflowBefore = (await host.WorkflowAsync(id)).GetProperty("stage").GetString();
        var statusBefore = await host.StatusAsync(id);

        await LetAutomaticPassesRunAsync(host);

        Assert.Equal(modelCallsBefore, host.ToolClient.Calls);
        Assert.Empty((await host.ProposalsAsync(id)).EnumerateArray());
        Assert.Equal(workflowBefore, (await host.WorkflowAsync(id)).GetProperty("stage").GetString());
        Assert.Equal(statusBefore, await host.StatusAsync(id));
        var types = ProposalDecisionHttp.AuditTypes(await host.AuditAsync(id));
        Assert.DoesNotContain("ProposalGenerated", types);
        Assert.Equal(expectedDecisions, types.Count(t => t == "DesignDecided"));
    }

    [Fact]
    public async Task AnUndecidedDesign_GetsNoProposal_AndNoDecision_FromTheAutomaticPath()
    {
        using var host = new ProposalHost(settings: FastOn, useFakeBasis: false);
        var id = await AutomaticallyDraftedUndecidedDesignAsync(host);

        await AssertAutomaticPathDidNothingAsync(host, id, expectedDecisions: 0);
    }

    [Fact]
    public async Task ARejectedDesign_GetsNoProposal_AndNoNewDecision_FromTheAutomaticPath()
    {
        using var host = new ProposalHost(settings: FastOn, useFakeBasis: false);
        var id = await AutomaticallyDraftedUndecidedDesignAsync(host);
        await host.RejectDesignAsync(id, 1);

        await AssertAutomaticPathDidNothingAsync(host, id, expectedDecisions: 1);
    }
}
