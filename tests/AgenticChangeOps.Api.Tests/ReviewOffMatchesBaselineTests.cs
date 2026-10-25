using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// FR-019 and FR-020: with the Reviewer unused every existing response is identical to before, and no
/// HTTP operation, tag or schema mentions reviews. The recorded status, header and body transcripts and
/// the generated API description are held by the existing golden tests (see
/// <c>specs/018-reviewer-agent-review/baseline/README.md</c>), which must pass unchanged with the Reviewer
/// registered; these tests add the facts no golden covers.
/// </summary>
public sealed class ReviewOffMatchesBaselineTests
{
    private sealed class CountingReviewGenerator : IReviewGenerator
    {
        private int _calls;

        public int Calls => _calls;

        public Task<ReviewContent?> GenerateAsync(
            ChangeRequest changeRequest,
            ReviewRequirementsBasis requirements,
            ReviewDesignBasis design,
            ReviewProposalBasis proposal,
            SystemCheck systemCheck,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult<ReviewContent?>(new ReviewContent(Array.Empty<ReviewFindingContent>(), Array.Empty<string>()));
        }
    }

    [Fact]
    public void NoRoute_MentionsReviews()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, r => r.Contains("review", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheApiDescription_MentionsNoReviewRouteTagOrSchema()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("review", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ASubmittedRequest_AndTheExistingRoutes_NeverTouchTheReviewer()
    {
        var counting = new CountingReviewGenerator();
        using var host = new ReviewHost(generator: counting);

        var id = await host.ReadyForReviewAsync();
        var workflow = await host.Client.GetAsync($"/change-requests/{id}/workflow");
        var advance = await host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);
        var audit = await host.AuditJsonAsync(id);

        Assert.Equal(HttpStatusCode.OK, workflow.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode); // nothing due: proposals are not workflow steps
        Assert.Equal(0, counting.Calls);
        Assert.Equal(0, host.ReviewClient.CallCount);
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
        Assert.DoesNotContain("ReviewGenerated", audit);
    }

    [Fact]
    public async Task TheWorkflowStatus_HasNoReviewMember_AtAnyStageOfTheFullChain()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();

        var body = await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("review", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARegisteredReviewer_AddsNothingToTheAuditListing_UntilSomeoneAsksForAReview()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var before = await host.AuditJsonAsync(id);

        _ = host.Services.GetRequiredService<IReviewService>(); // obtaining the service is not a review
        var after = await host.AuditJsonAsync(id);

        Assert.Equal(before, after);
    }
}
