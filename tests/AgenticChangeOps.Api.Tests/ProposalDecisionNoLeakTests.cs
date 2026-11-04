using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Both new refusals carry only their fixed text: no endpoint, deployment name, sample path or
/// content, and the refusal writes no log line that could hold any of them.
/// </summary>
public sealed class ProposalDecisionNoLeakTests
{
    [Theory]
    [InlineData(ProposalDesignVerdict.None)]
    [InlineData(ProposalDesignVerdict.Rejected)]
    public async Task ARefusal_ContainsNoSetting_Path_OrContent_InTheBodyOrTheLogs(ProposalDesignVerdict verdict)
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(id, FakeProposalBasis.Current(decision: verdict));

        var response = await host.ProposeAsync(id);

        var text = (await response.ReadJsonAsync()).ToString() + "\n" + string.Join("\n", host.Logs.Lines);
        foreach (var marker in new[]
                 {
                     ProposalHost.Endpoint, ProposalHost.Deployment, host.SampleFolder,
                     "orders.page.size", "orders-service/", "Raise the number of orders shown per page"
                 })
            Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);

        // One line, naming only the refused rule: no id, message text, name, path or setting.
        var lines = host.Logs.Lines
            .Where(l => l.Contains("Proposal request stored nothing", StringComparison.Ordinal)).ToArray();
        var line = Assert.Single(lines);
        Assert.Contains("DesignAcceptance", line);
        Assert.DoesNotContain(id.ToString(), line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sam Sample", line);
        Assert.DoesNotContain("decided", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rejected (", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ASuccessfulProposal_ForAnAcceptedDesign_WritesNoStoredNothingLine()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(id, FakeProposalBasis.Current(decision: ProposalDesignVerdict.Accepted));

        var response = await host.ProposeAsync(id);

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain(host.Logs.Lines, l => l.Contains("Proposal request stored nothing", StringComparison.Ordinal));
    }
}
