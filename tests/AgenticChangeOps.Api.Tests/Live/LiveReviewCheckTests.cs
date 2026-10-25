using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.SampleCode;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Live;

/// <summary>
/// Manual, opt-in check against the real model. Skipped unless AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1; needs
/// the model settings and a signed-in identity (no sample tools: the Reviewer uses none). It builds the
/// generator through the host's composition and asks the real model for a review of a small synthetic chain, then checks only structure: the answer parses,
/// every finding passes the shape rules, every reference resolves, and the system check is the one the
/// system computed. It writes nothing from the prompt, the answer, the endpoint or the deployment name.
/// Not run as part of the routine suite.
/// </summary>
public class LiveReviewCheckTests
{
    private static readonly ChangeRequest SyntheticRequest = new(
        ChangeRequestId: Guid.NewGuid(),
        Title: "Raise the order page size",
        Description: "Raise the number of orders shown per page in the orders service.",
        Requester: "Avery Quill",
        AffectedSystem: "Orders Service",
        Priority: Priority.Medium,
        ChangeType: ChangeType.ConfigChange,
        Status: ChangeRequestStatus.Approved,
        SubmittedAt: DateTime.UtcNow);

    private static ReviewBases SyntheticBases() => new(
        new ReviewRequirementsBasis(
            1, "Raise the number of orders shown per page.",
            new[] { "The page size is configurable.", "The new size applies to every orders page." },
            new[] { "Pages show the new size.", "No page shows more than 100 orders." },
            new[] { "Only the orders service is affected." },
            new[] { "Should the maximum be capped?" }),
        new ReviewDesignBasis(
            1, 1, "Change the page size setting only.",
            new[] { "Orders settings" }, new[] { "Longer pages load more rows." }, Array.Empty<string>(),
            new[] { "Restore the previous page size." }, Array.Empty<string>()),
        new ReviewProposalBasis(
            1, 1, 1, "Raise the orders page size from 25 to 50.",
            new[]
            {
                new ReviewProposedChange(ProposalHost.OrdersSettings, ReviewChangeKind.Change, "orders.page.size = 25", "orders.page.size = 50")
            },
            new[] { "Longer pages load more rows." }, new[] { "Check a page shows fifty rows." }, new[] { "Should there be a maximum?" }));

    [LiveModelFact]
    public async Task RealModel_WritesAReviewThatParsesAndWhoseReferencesResolve()
    {
        // The real generator, built by the host's own composition from the process environment (the model
        // settings), exactly as production builds it. If the model is not configured this is the
        // not-configured generator and the call below fails with a category, which is what to fix.
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var generator = factory.Services.GetRequiredService<IReviewGenerator>();

        var sample = SampleToolsHost.RealSampleFolder();
        var bases = SyntheticBases();
        var check = new SampleChecker(new ReviewSampleFilesAdapter(sample, SampleToolSettings.DefaultMaxFileBytes))
            .Check(bases.Proposal!.Changes);

        var content = await generator.GenerateAsync(
            SyntheticRequest, bases.Requirements!, bases.Design!, bases.Proposal!, check, CancellationToken.None);

        Assert.NotNull(content);
        var validation = new ReviewContentValidator(ReviewLimits.Defaults).Validate(content, bases);
        Assert.Empty(validation.Errors);
        Assert.True(validation.Rejection is null, "The review was rejected: a reference, count or size rule was broken.");
        Assert.All(validation.Value!.Findings, finding => Assert.NotEmpty(finding.References));
        Assert.Equal(SystemCheckStatus.Performed, check.Status);
        Assert.Equal(EditCheckOutcome.Found, Assert.Single(check.Edits).Outcome);
    }
}
