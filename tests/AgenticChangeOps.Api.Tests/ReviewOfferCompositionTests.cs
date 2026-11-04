using System.Net;
using System.Text.Json;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Slice 021, end to end through the real composition: the workflow status offers the review once a
/// current proposal exists, and the offer is information only.
/// </summary>
public sealed class ReviewOfferCompositionTests
{
    private static readonly Dictionary<string, string?> ReviewingUnconfigured = new()
    {
        ["AGENTICCHANGEOPS_MODEL_ENDPOINT"] = null,
        ["AGENTICCHANGEOPS_MODEL_DEPLOYMENT"] = null
    };

    private static async Task<JsonElement> StatusAsync(ReviewHost host, Guid id) =>
        await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();

    private static bool HasOffer(JsonElement status, out string? offered)
    {
        var present = status.TryGetProperty("offeredStep", out var value);
        offered = present ? value.GetString() : null;
        return present;
    }

    [Fact]
    public async Task AfterAProposal_TheStatusOffersTheReview_WithoutMakingItTheNextStep()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForProposalAsync();
        var before = await StatusAsync(host, id);
        Assert.True(HasOffer(before, out var beforeOffer));
        Assert.Equal("Proposal", beforeOffer);

        (await host.Client.PostAsync($"/change-requests/{id}/proposals", null)).EnsureSuccessStatusCode();
        var status = await StatusAsync(host, id);

        Assert.True(HasOffer(status, out var offered));
        Assert.Equal("Review", offered);
        Assert.Equal("DesignComplete", status.GetProperty("stage").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("nextStep").ValueKind);
        Assert.Equal(before.GetProperty("reason").GetRawText(), status.GetProperty("reason").GetRawText());
        Assert.Equal(before.GetProperty("completedSteps").GetRawText(), status.GetProperty("completedSteps").GetRawText());
    }

    [Fact]
    public async Task AfterTheReview_NothingFurtherIsOffered()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        Assert.True(HasOffer(await StatusAsync(host, id), out var offered));
        Assert.Equal("Review", offered);

        (await host.RequestReviewAsync(id)).EnsureSuccessStatusCode();

        Assert.False(HasOffer(await StatusAsync(host, id), out _));
    }

    [Fact]
    public async Task ANewerDesignAndProposal_OfferTheReviewAgain_AndTheOldReviewDoesNotSuppressIt()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        (await host.RequestReviewAsync(id)).EnsureSuccessStatusCode();

        // A newer design makes the stored proposal and review outdated: the proposal is offered again.
        (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();
        Assert.True(HasOffer(await StatusAsync(host, id), out var afterDesign));
        Assert.Equal("Proposal", afterDesign);

        // A proposal on the newer design is current and has no review of it (the person accepts it first).
        (await host.Client.PostJsonAsync($"/change-requests/{id}/designs/2/decisions",
            new { approverName = "Sam Sample", decision = "Accept", reason = (string?)null })).EnsureSuccessStatusCode();
        (await host.Client.PostAsync($"/change-requests/{id}/proposals", null)).EnsureSuccessStatusCode();
        Assert.True(HasOffer(await StatusAsync(host, id), out var afterProposal));
        Assert.Equal("Review", afterProposal);
    }

    [Fact]
    public async Task WithReviewingNotConfigured_TheStatusStillWorks_AndShowsNoReviewOffer()
    {
        using var host = new ReviewHost(settings: new(ReviewingUnconfigured), toolsOn: false, modelConfigured: false);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var response = await host.Client.GetAsync($"/change-requests/{id}/workflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.ReadJsonAsync();
        Assert.False(HasOffer(status, out _));
        Assert.Equal("DesignComplete", status.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task WithReviewingConfiguredButProposalsOff_ACurrentProposalStillOffersTheReview()
    {
        using var host = new ReviewHost(toolsOn: false);
        var id = await host.ReadyForProposalAsync();
        Assert.False(HasOffer(await StatusAsync(host, id), out _));

        host.SeedProposal(id);

        Assert.True(HasOffer(await StatusAsync(host, id), out var offered));
        Assert.Equal("Review", offered);
    }

    [Fact]
    public async Task Advancing_WhenTheOnlyOfferIsTheReview_ReportsNothingDue_AndRunsNoModel()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var reviewCalls = host.ReviewClient.CallCount;
        var toolCalls = host.ToolClient.Calls;
        var audit = await host.AuditJsonAsync(id);

        var advance = await host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);

        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        Assert.Equal(reviewCalls, host.ReviewClient.CallCount);
        Assert.Equal(toolCalls, host.ToolClient.Calls);
        Assert.Equal(audit, await host.AuditJsonAsync(id));
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
    }

    [Theory]
    [InlineData("not-a-guid", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-0000000000aa", HttpStatusCode.NotFound)]
    public async Task AMalformedOrUnknownId_BehavesAsBefore(string segment, HttpStatusCode expected)
    {
        using var host = new ReviewHost();

        var response = await host.Client.GetAsync($"/change-requests/{segment}/workflow");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task ReadingTheStatus_WritesNothing_AndLeaksNothing()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var audit = await host.AuditJsonAsync(id);
        var proposals = host.Store.GetAgentOutputs<AgenticChangeOps.Agents.Developer.ChangeProposal>(id).Count;
        var requestStatus = await ReviewHttp.StatusAsync(host, id);

        string body = "";
        for (var i = 0; i < 3; i++)
            body = await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync();

        Assert.Equal(audit, await host.AuditJsonAsync(id));
        Assert.Equal(proposals, host.Store.GetAgentOutputs<AgenticChangeOps.Agents.Developer.ChangeProposal>(id).Count);
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(requestStatus, await ReviewHttp.StatusAsync(host, id));

        var everything = body + "\n" + string.Join("\n", host.Logs.Lines);
        Assert.DoesNotContain(ReviewHost.Endpoint, everything);
        Assert.DoesNotContain(ReviewHost.Deployment, everything);
        Assert.DoesNotContain("Synthetic finding", everything);
        Assert.DoesNotContain("orders.page.size", everything);
    }
}
