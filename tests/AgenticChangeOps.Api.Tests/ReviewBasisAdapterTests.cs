using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// The adapter that gives the Reviewer the latest requirements, design and proposal through the other
/// agents own services, copying their sections into the Reviewer's own read model.
/// </summary>
public sealed class ReviewBasisAdapterTests
{
    [Fact]
    public async Task WithTheFullChain_EveryPartIsCopiedFieldForField()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var draft = host.Services.GetRequiredService<IRequirementsDraftingService>().GetLatestDraft(id).Value!;
        var design = host.Services.GetRequiredService<IDesignDraftingService>().GetLatestDesign(id).Value!;
        var proposal = host.Services.GetRequiredService<IProposalService>().GetLatestProposal(id).Value!;

        var bases = host.Services.GetRequiredService<IReviewBasisSource>().GetLatest(id);

        var r = bases.Requirements!;
        Assert.Equal(draft.Version, r.Version);
        Assert.Equal(draft.Summary, r.Summary);
        Assert.Equal(draft.FunctionalRequirements, r.FunctionalRequirements);
        Assert.Equal(draft.AcceptanceCriteria, r.AcceptanceCriteria);
        Assert.Equal(draft.Assumptions, r.Assumptions);
        Assert.Equal(draft.OpenQuestions, r.OpenQuestions);

        var d = bases.Design!;
        Assert.Equal(design.Version, d.Version);
        Assert.Equal(design.RequirementsVersion, d.RequirementsVersion);
        Assert.Equal(design.ApproachSummary, d.ApproachSummary);
        Assert.Equal(design.AffectedComponents, d.AffectedComponents);
        Assert.Equal(design.ImpactAndRisks, d.ImpactAndRisks);
        Assert.Equal(design.Dependencies, d.Dependencies);
        Assert.Equal(design.RollbackConsiderations, d.RollbackConsiderations);
        Assert.Equal(design.OpenQuestions, d.OpenQuestions);

        var p = bases.Proposal!;
        Assert.Equal(proposal.Version, p.Version);
        Assert.Equal(proposal.RequirementsVersion, p.RequirementsVersion);
        Assert.Equal(proposal.DesignVersion, p.DesignVersion);
        Assert.Equal(proposal.Summary, p.Summary);
        Assert.Equal(proposal.Risks, p.Risks);
        Assert.Equal(proposal.TestIdeas, p.TestIdeas);
        Assert.Equal(proposal.OpenQuestions, p.OpenQuestions);
        Assert.Equal(proposal.Changes.Count, p.Changes.Count);
    }

    [Fact]
    public async Task EachEdit_KeepsItsPathKindAndText_AndAnAddHasNoQuote()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        var proposal = host.Services.GetRequiredService<IProposalService>().GetLatestProposal(id).Value!;

        var edits = host.Services.GetRequiredService<IReviewBasisSource>().GetLatest(id).Proposal!.Changes;

        Assert.Equal(proposal.Changes.Count, edits.Count);
        for (var i = 0; i < edits.Count; i++)
        {
            Assert.Equal(proposal.Changes[i].Path, edits[i].Path);
            Assert.Equal(proposal.Changes[i].ReplacementText, edits[i].ReplacementText);
            Assert.Equal(proposal.Changes[i].ExistingText, edits[i].ExistingText);
            Assert.Equal(
                proposal.Changes[i].Kind == ProposalChangeKind.Add ? ReviewChangeKind.Add : ReviewChangeKind.Change,
                edits[i].Kind);
        }
        Assert.Contains(edits, e => e.Kind == ReviewChangeKind.Add && e.ExistingText is null);
        Assert.Contains(edits, e => e.Kind == ReviewChangeKind.Change && e.ExistingText is not null);
    }

    [Fact]
    public async Task ThePartsThatDoNotExist_AreNull()
    {
        using var host = new ReviewHost();
        var id = await host.Client.SubmitAsync();

        var none = host.Services.GetRequiredService<IReviewBasisSource>().GetLatest(id);
        Assert.Null(none.Requirements);
        Assert.Null(none.Design);
        Assert.Null(none.Proposal);

        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        var draftOnly = host.Services.GetRequiredService<IReviewBasisSource>().GetLatest(id);
        Assert.NotNull(draftOnly.Requirements);
        Assert.Null(draftOnly.Design);
        Assert.Null(draftOnly.Proposal);
    }

    [Fact]
    public void AnUnknownChangeRequest_GivesThreeNullParts()
    {
        using var host = new ReviewHost();

        var bases = host.Services.GetRequiredService<IReviewBasisSource>().GetLatest(Guid.NewGuid());

        Assert.Null(bases.Requirements);
        Assert.Null(bases.Design);
        Assert.Null(bases.Proposal);
    }

    [Fact]
    public async Task TheLatestOfEachIsUsed_WhenMoreThanOneVersionExists()
    {
        using var host = new ReviewHost();
        var id = await host.ReadyForReviewAsync();
        (await host.Client.PostAsync($"/change-requests/{id}/proposals", null)).EnsureSuccessStatusCode();

        var bases = host.Services.GetRequiredService<IReviewBasisSource>().GetLatest(id);

        Assert.Equal(2, bases.Proposal!.Version);
    }
}
