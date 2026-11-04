using System.Net;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>US1 to US3 over HTTP, with a fake basis carrying the verdict of the latest design.</summary>
public sealed class ProposalDecisionEndpointTests
{
    private static async Task<Guid> ApprovedWithVerdictAsync(ProposalHost host, ProposalDesignVerdict verdict)
    {
        var id = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(id, FakeProposalBasis.Current(decision: verdict));
        return id;
    }

    private static async Task AssertNothingHappenedAsync(ProposalHost host, Guid id)
    {
        Assert.Equal(0, host.ToolClient.Calls);
        Assert.Empty((await host.ProposalsAsync(id)).EnumerateArray());
        var types = ProposalDecisionHttp.AuditTypes(await host.AuditAsync(id));
        Assert.DoesNotContain("ProposalGenerated", types);
        Assert.DoesNotContain("ToolCalled", types);
        Assert.Equal("Approved", await host.StatusAsync(id));
    }

    [Fact]
    public async Task AnAcceptedDesign_GivesTheSame201AsBefore()
    {
        using var host = new ProposalHost();
        var id = await ApprovedWithVerdictAsync(host, ProposalDesignVerdict.Accepted);

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/change-requests/{id}/proposals/latest", response.Headers.Location!.OriginalString);
        var body = await response.ReadJsonAsync();
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal(1, body.GetProperty("designVersion").GetInt32());
    }

    [Fact]
    public async Task AnUndecidedDesign_Is409Conflict_NamingTheDecision_AndNothingHappens()
    {
        using var host = new ProposalHost();
        var id = await ApprovedWithVerdictAsync(host, ProposalDesignVerdict.None);

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("conflict", body.GetProperty("code").GetString());
        Assert.Equal(new[] { "DesignAcceptance" }, ProposalDecisionHttp.ErrorFields(body));
        Assert.Contains("not been decided", body.GetProperty("errors")[0].GetProperty("message").GetString());
        await AssertNothingHappenedAsync(host, id);
    }

    [Fact]
    public async Task ARejectedDesign_Is409Conflict_WithADifferentMessage_AndNothingHappens()
    {
        using var host = new ProposalHost();
        var undecided = await ApprovedWithVerdictAsync(host, ProposalDesignVerdict.None);
        var rejected = await ApprovedWithVerdictAsync(host, ProposalDesignVerdict.Rejected);

        var first = await (await host.ProposeAsync(undecided)).ReadJsonAsync();
        var response = await host.ProposeAsync(rejected);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("conflict", body.GetProperty("code").GetString());
        Assert.Equal(new[] { "DesignAcceptance" }, ProposalDecisionHttp.ErrorFields(body));
        var message = body.GetProperty("errors")[0].GetProperty("message").GetString();
        Assert.Contains("was rejected", message);
        Assert.NotEqual(first.GetProperty("errors")[0].GetProperty("message").GetString(), message);
        await AssertNothingHappenedAsync(host, rejected);
    }

    [Fact]
    public async Task TheRefusal_HasTheSameShapeAsTheOtherProposalConflicts()
    {
        using var host = new ProposalHost();
        var undecided = await ApprovedWithVerdictAsync(host, ProposalDesignVerdict.None);
        var noDesign = await host.ApprovedAsync(withDrafts: false);

        var decisionBody = await (await host.ProposeAsync(undecided)).ReadJsonAsync();
        var otherBody = await (await host.ProposeAsync(noDesign)).ReadJsonAsync();

        Assert.Equal(
            otherBody.EnumerateObject().Select(p => p.Name).OrderBy(n => n),
            decisionBody.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal(otherBody.GetProperty("code").GetString(), decisionBody.GetProperty("code").GetString());
        Assert.Equal(otherBody.GetProperty("message").GetString(), decisionBody.GetProperty("message").GetString());
    }
}
