using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// contracts/settings-and-failures.md "Logging": a proposal request that stores nothing logs one
/// line with the failure category or rejection reason, and never a path, quote, model text,
/// endpoint, deployment name or setting value.
/// </summary>
public sealed class ProposalLoggingTests
{
    private const string Secret = "SECRET-LOG-QUOTE-0123";

    private static string[] ProposalLines(ProposalHost host) =>
        host.Logs.Lines.Where(l => l.Contains("Proposal request stored nothing", StringComparison.Ordinal)).ToArray();

    private static void AssertNoContent(ProposalHost host)
    {
        var all = string.Join("\n", host.Logs.Lines);
        foreach (var marker in new[]
                 {
                     Secret, ProposalHost.Endpoint, ProposalHost.Deployment, "orders.page.size", "orders-service/",
                     host.SampleFolder
                 })
            Assert.DoesNotContain(marker, all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARejectedProposal_LogsOneLine_WithTheReasonName_AndNoContent()
    {
        using var host = new ProposalHost(behavior: ProposalHost.ReadsAndProposes(
            ProposalHost.ChangeEdit(existing: Secret), ProposalHost.AddEdit(path: "../" + Secret)));
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var line = Assert.Single(ProposalLines(host));
        Assert.Contains("QuoteNotFound", line);
        AssertNoContent(host);
    }

    [Fact]
    public async Task AModelFailure_LogsOneLine_WithTheCategoryName_AndNoContent()
    {
        using var host = new ProposalHost(behavior: ProposalHost.Throws(
            new DraftGenerationException(GenerationFailureCategory.TimedOut)));
        var id = await host.ApprovedAsync();

        await host.ProposeAsync(id);

        var line = Assert.Single(ProposalLines(host));
        Assert.Contains("TimedOut", line);
        AssertNoContent(host);
    }

    [Fact]
    public async Task ARefusal_LogsOneLine_NamingTheRule_NotTheContent()
    {
        using var host = new ProposalHost();
        var id = await host.SubmittedAsync();   // not Approved

        await host.ProposeAsync(id);

        var line = Assert.Single(ProposalLines(host));
        Assert.Contains("Status", line);
        AssertNoContent(host);
    }

    [Fact]
    public async Task ASuccessfulRequest_LogsNoStoredNothingLine()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(ProposalLines(host));
    }

    [Fact]
    public async Task WhenTheDeveloperIsUnused_NothingIsLoggedAboutProposals()
    {
        using var host = new ProposalHost(toolsOn: false);
        var id = await host.ApprovedAsync();

        await host.Client.GetAsync($"/change-requests/{id}/proposals");

        Assert.Empty(ProposalLines(host));
    }
}
