using System.Text.Json;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// End to end through the real composition: requirements, design and proposal all go through the real
/// services and adapters, the system re-checks the proposal against a throwaway copy of the real sample,
/// and only the model calls are scripted. SC-001: every edit of a real proposal is found or new-file-free.
/// </summary>
public sealed class ReviewCompositionTests
{
    private static async Task<(ReviewHost Host, Guid Id)> ReadyAsync(string system, ReviewHost? host = null)
    {
        host ??= new ReviewHost();
        var id = await host.ReadyForReviewAsync(system);
        return (host, id);
    }

    private static async Task<string> StatusAsync(ReviewHost host, Guid id) =>
        (await (await host.Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync()).GetProperty("status").GetString()!;

    // ---- the full chain ---------------------------------------------------------------

    [Fact]
    public async Task AnOrdersProposal_IsReviewed_AndEveryEditChecksOutAgainstTheRealSample()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;
        var fingerprint = host.SampleFingerprint();

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        var review = result.Review!;
        Assert.Equal(1, review.Version);
        Assert.Equal(ReviewStatus.Draft, review.Status);
        Assert.Equal(SystemCheckStatus.Performed, review.SystemCheck.Status);
        Assert.NotEmpty(review.SystemCheck.Edits);
        Assert.All(review.SystemCheck.Edits, e => Assert.True(
            e.Outcome is EditCheckOutcome.Found or EditCheckOutcome.NewFileFree, $"edit {e.ChangeNumber}: {e.Outcome}"));
        Assert.Equal(1, host.ReviewClient.CallCount);
        Assert.Equal(fingerprint, host.SampleFingerprint()); // read-only: the sample is byte-for-byte identical
    }

    [Fact]
    public async Task ABillingProposal_IsReviewedToo_AgainstTheBillingPortalSettings()
    {
        var (host, id) = await ReadyAsync("Billing Portal");
        using var _ = host;
        var proposalResult = await host.Client.PostAsync($"/change-requests/{id}/proposals", null);
        proposalResult.EnsureSuccessStatusCode();

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.All(result.Review!.SystemCheck.Edits, e => Assert.True(
            e.Outcome is EditCheckOutcome.Found or EditCheckOutcome.NewFileFree, $"edit {e.ChangeNumber}: {e.Outcome}"));
    }

    [Fact]
    public async Task TheStoredReview_IsLinkedToTheVersionsOfTheRealRecords_AndAudited()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;

        var review = (await host.Reviewing.ReviewAsync(id)).Review!;

        Assert.Equal(1, review.RequirementsVersion);
        Assert.Equal(1, review.DesignVersion);
        Assert.Equal(1, review.ProposalVersion);
        var audit = JsonDocument.Parse(await host.AuditJsonAsync(id)).RootElement;
        var types = audit.EnumerateArray().Select(e => e.GetProperty("eventType").ToString()).ToList();
        Assert.Single(types, t => t == "ReviewGenerated");
        Assert.Equal(ChangeRequestStatus.Approved.ToString(), (await StatusAsync(host, id)));
    }

    [Fact]
    public async Task TheAdvisoryReview_ChangesNoStatus()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;
        var before = await StatusAsync(host, id);
        var workflowBefore = await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync();

        await host.Reviewing.ReviewAsync(id);

        Assert.Equal(before, await StatusAsync(host, id));
        Assert.Equal(workflowBefore, await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TwoSequentialRequests_GiveVersionsOneAndTwo_AndLatestAndHistoryAgree()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;

        var first = (await host.Reviewing.ReviewAsync(id)).Review!;
        var second = (await host.Reviewing.ReviewAsync(id)).Review!;

        Assert.Equal((1, 2), (first.Version, second.Version));
        Assert.Equal(second, host.Reviewing.GetLatestReview(id).Value);
        Assert.Equal(new[] { 1, 2 }, host.Reviewing.GetReviewHistory(id).Value!.Select(r => r.Version));
        Assert.Equal(first, host.Reviewing.GetReviewHistory(id).Value![0]);
    }

    // ---- no tools switch needed -------------------------------------------------------

    [Fact]
    public async Task ReviewsWork_WithTheSampleToolsSwitchedOff()
    {
        using var host = new ReviewHost(toolsOn: false);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemCheckStatus.Performed, result.Review!.SystemCheck.Status);
        Assert.Equal(EditCheckOutcome.Found, result.Review.SystemCheck.Edits[0].Outcome);
        Assert.Equal(0, host.ToolClient.Calls); // the Reviewer uses no tools and the Developer is not involved
    }

    [Fact]
    public async Task TheReviewerNeverOffersATool_ItsModelCallIsAPlainChatCall()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;
        var toolCallsBefore = host.ToolClient.Calls;

        await host.Reviewing.ReviewAsync(id);

        Assert.Equal(toolCallsBefore, host.ToolClient.Calls);
        Assert.Equal(1, host.ReviewClient.CallCount);
    }

    // ---- sample unavailable -----------------------------------------------------------

    [Fact]
    public async Task WithTheSampleFolderMissing_TheReviewIsStored_WithTheCheckNotPerformed()
    {
        var missing = Path.Combine(Path.GetTempPath(), "changeops-review-gone-" + Guid.NewGuid().ToString("N"));
        using var host = new ReviewHost(toolsOn: false, sampleFolder: missing);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemCheckStatus.NotPerformed, result.Review!.SystemCheck.Status);
        Assert.Empty(result.Review.SystemCheck.Edits);
        Assert.False(Directory.Exists(missing));
    }

    // ---- not configured ---------------------------------------------------------------

    [Fact]
    public async Task WithInvalidModelSettings_TheServiceFailsBeforeAnyModelCall_AndStoresNothing()
    {
        using var host = new ReviewHost(toolsOn: false, modelConfigured: false);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.Unreachable, result.FailureCategory);
        Assert.Equal(0, host.ReviewClient.CallCount);
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
    }

    // ---- failures and leaks -----------------------------------------------------------

    [Fact]
    public async Task AModelFailure_StoresNothing_AndLeaksNothing()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;
        host.ReviewClient.Behavior = ScriptedChatModelClient.Throws(
            new InvalidOperationException("boom at " + ReviewHost.Endpoint + " " + ReviewHost.Deployment + " HOSTILE-MARKER-0001"));

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
        var everything = string.Join("\n", host.Logs.Lines) + string.Join("|", result.Errors.Select(e => e.Message))
            + (result.FailureCategory?.ToString() ?? string.Empty) + await host.AuditJsonAsync(id);
        Assert.DoesNotContain("sentinel-endpoint", everything);
        Assert.DoesNotContain("sentinel-deployment", everything);
        Assert.DoesNotContain("HOSTILE-MARKER-0001", everything);
    }

    [Fact]
    public async Task ADanglingReference_StoresNothing_AndNoLogOrAuditCarriesTheFindingText()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;
        host.ReviewClient.Behavior = ScriptedChatModelClient.ReturnsText("""
            { "findings": [ { "category": "UnmentionedRisk", "severity": "High",
                              "statement": "HOSTILE-MARKER-0001 secret finding", "references": ["proposal.changes[99]"] } ],
              "openQuestions": [] }
            """);

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.Equal(ReviewRejection.UnresolvedReference, result.Rejection);
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
        var everything = string.Join("\n", host.Logs.Lines) + await host.AuditJsonAsync(id);
        Assert.DoesNotContain("HOSTILE-MARKER-0001", everything);
        Assert.DoesNotContain("proposal.changes[99]", everything);
    }

    [Fact]
    public async Task TheLogs_NeverCarryAnythingFromASuccessfulReviewEither()
    {
        var (host, id) = await ReadyAsync("Orders Service");
        using var _ = host;

        await host.Reviewing.ReviewAsync(id);

        var logs = string.Join("\n", host.Logs.Lines);
        Assert.DoesNotContain("Synthetic finding about a risk.", logs);
        Assert.DoesNotContain("sentinel-endpoint", logs);
        Assert.DoesNotContain("orders.page.size", logs);
        Assert.DoesNotContain(host.SampleFolder, logs);
    }
}
