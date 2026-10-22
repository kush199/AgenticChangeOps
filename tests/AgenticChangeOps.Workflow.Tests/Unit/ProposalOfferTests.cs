using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

/// <summary>
/// Slice 016, US5: the workflow may offer the proposal as a person-requested step once a current
/// design exists. The offer is an extra, optional member; the stage, the next step, the reason and
/// the completed steps are exactly what they were.
/// </summary>
public class ProposalOfferTests
{
    private sealed class Offer : IProposalOffer
    {
        public bool IsOffered { get; set; }
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WorkflowService WithOffer(WorkflowTestFixture f, IProposalOffer? offer) =>
        new(f.Store, f.Requirements, f.Designs, f.Availability, null, offer);

    private static async Task<(WorkflowTestFixture F, Guid Id)> DesignCompleteAsync()
    {
        var f = new WorkflowTestFixture();
        var id = await f.ApprovedWithDraftAsync();
        Assert.Equal(AdvanceOutcome.Advanced, (await f.Workflow.AdvanceAsync(id)).Outcome);
        return (f, id);
    }

    [Fact]
    public async Task WithNoOffer_TheStatusAtDesignComplete_IsExactlyToday_AndHasNoOfferedStep()
    {
        var (f, id) = await DesignCompleteAsync();

        var status = f.Workflow.GetStatus(id).Value!;

        Assert.Equal(WorkflowStage.DesignComplete, status.Stage);
        Assert.Null(status.NextStep);
        Assert.Equal(WorkflowReasonCode.NothingDue, status.Reason!.Code);
        Assert.Null(status.OfferedStep);
        Assert.DoesNotContain("offeredStep", JsonSerializer.Serialize(status, Web));
    }

    [Fact]
    public async Task AnOfferThatIsOff_ChangesNothing()
    {
        var (f, id) = await DesignCompleteAsync();
        var plain = f.Workflow.GetStatus(id).Value!;

        var withOff = WithOffer(f, new Offer { IsOffered = false }).GetStatus(id).Value!;

        Assert.Equal(plain, withOff);
        Assert.Equal(JsonSerializer.Serialize(plain, Web), JsonSerializer.Serialize(withOff, Web));
    }

    [Fact]
    public async Task AnOfferThatIsOn_AddsOnlyTheOfferedStep_AtDesignComplete()
    {
        var (f, id) = await DesignCompleteAsync();
        var plain = f.Workflow.GetStatus(id).Value!;

        var offered = WithOffer(f, new Offer { IsOffered = true }).GetStatus(id).Value!;

        Assert.Equal(WorkflowStep.Proposal, offered.OfferedStep);
        Assert.Equal(plain.Stage, offered.Stage);
        Assert.Equal(plain.NextStep, offered.NextStep);
        Assert.Equal(plain.Reason, offered.Reason);
        Assert.Equal(plain.CompletedSteps, offered.CompletedSteps);
        Assert.Contains("\"offeredStep\":\"Proposal\"", JsonSerializer.Serialize(offered, Web));
    }

    [Fact]
    public async Task TheOfferedStep_IsOnlyShownAtDesignComplete()
    {
        var f = new WorkflowTestFixture();
        var service = WithOffer(f, new Offer { IsOffered = true });

        var submitted = f.SubmitSynthetic();
        Assert.Null(service.GetStatus(submitted).Value!.OfferedStep);

        var drafted = await f.SubmittedWithDraftAsync();
        Assert.Null(service.GetStatus(drafted).Value!.OfferedStep);

        var approved = await f.ApprovedWithDraftAsync();
        Assert.Equal(WorkflowStage.ReadyForDesign, service.GetStatus(approved).Value!.Stage);
        Assert.Null(service.GetStatus(approved).Value!.OfferedStep);

        foreach (var status in new[] { ChangeRequestStatus.Rejected, ChangeRequestStatus.Deferred })
        {
            var other = await f.SubmittedWithDraftAsync();
            f.SeedStatus(other, status);
            Assert.Null(service.GetStatus(other).Value!.OfferedStep);
        }
    }

    [Fact]
    public async Task AdvancingAtDesignComplete_StillFindsNothingDue_EvenWhenTheProposalIsOffered()
    {
        var (f, id) = await DesignCompleteAsync();
        var service = WithOffer(f, new Offer { IsOffered = true });
        var auditBefore = f.Store.GetAuditEntries(id).Count;

        var result = await service.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.NothingDue, result.Outcome);
        Assert.Null(result.Step);
        Assert.Equal(WorkflowStep.Proposal, result.Status!.OfferedStep);
        Assert.Equal(auditBefore, f.Store.GetAuditEntries(id).Count);
    }

    [Fact]
    public void TheWorkflowStep_AppendsTheProposalValue_AfterTheExistingOnes()
    {
        Assert.Equal(0, (int)WorkflowStep.RequirementsDraft);
        Assert.Equal(1, (int)WorkflowStep.DesignDraft);
        Assert.Equal(2, (int)WorkflowStep.Proposal);
    }

    [Fact]
    public void TheWorkflowProject_DoesNotReferenceTheDeveloperAgent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");

        var project = File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "AgenticChangeOps.Workflow", "AgenticChangeOps.Workflow.csproj"));

        Assert.DoesNotContain("Agents.Developer", project);
    }
}
