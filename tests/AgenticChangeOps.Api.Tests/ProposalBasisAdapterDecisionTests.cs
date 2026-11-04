using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US4: the adapter reports the recorded verdict of the latest design version only, reading the
/// recorded decisions without writing anything.
/// </summary>
public sealed class ProposalBasisAdapterDecisionTests
{
    private static ProposalDesignVerdict VerdictOf(ProposalHost host, Guid id) =>
        host.Services.GetRequiredService<IProposalBasisSource>().GetLatest(id).Design!.Decision;

    [Fact]
    public async Task AnUndecidedLatestDesign_IsNone()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();

        Assert.Equal(ProposalDesignVerdict.None, VerdictOf(host, id));
    }

    [Fact]
    public async Task AnAcceptedLatestDesign_IsAccepted()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();
        await host.AcceptDesignAsync(id, 1);

        Assert.Equal(ProposalDesignVerdict.Accepted, VerdictOf(host, id));
    }

    [Fact]
    public async Task ARejectedLatestDesign_IsRejected()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();
        await host.RejectDesignAsync(id, 1);

        Assert.Equal(ProposalDesignVerdict.Rejected, VerdictOf(host, id));
    }

    [Fact]
    public async Task ADecisionOnAnOlderVersion_DoesNotCountForTheNewerDesign()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();
        await host.AcceptDesignAsync(id, 1);
        await host.DraftDesignAsync(id);

        Assert.Equal(2, host.Services.GetRequiredService<IProposalBasisSource>().GetLatest(id).Design!.Version);
        Assert.Equal(ProposalDesignVerdict.None, VerdictOf(host, id));
    }

    [Fact]
    public async Task ReadingTheVerdict_WritesNothing()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await host.RealChainWithUndecidedDesignAsync();
        await host.AcceptDesignAsync(id, 1);
        var auditBefore = ProposalDecisionHttp.AuditTypes(await host.AuditAsync(id));

        _ = VerdictOf(host, id);
        _ = VerdictOf(host, id);

        Assert.Equal(auditBefore, ProposalDecisionHttp.AuditTypes(await host.AuditAsync(id)));
    }
}
