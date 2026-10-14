using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Fakes;

/// <summary>Settable per-step configured state. Holds no setting value.</summary>
public sealed class FakeStepAvailability : IStepAvailability
{
    public bool RequirementsConfigured { get; set; } = true;
    public bool DesignConfigured { get; set; } = true;

    public bool IsConfigured(WorkflowStep step) =>
        step == WorkflowStep.RequirementsDraft ? RequirementsConfigured : DesignConfigured;
}

/// <summary>
/// Test-only stand-in for the API composition adapter: reads the latest requirements draft
/// through the real requirements service and copies it into the Architect's own read model.
/// </summary>
public sealed class TestRequirementsBasisSource : IRequirementsBasisSource
{
    private readonly IRequirementsDraftingService _requirements;

    public TestRequirementsBasisSource(IRequirementsDraftingService requirements) => _requirements = requirements;

    public RequirementsBasis? GetLatest(Guid changeRequestId)
    {
        var result = _requirements.GetLatestDraft(changeRequestId);
        if (!result.IsSuccess || result.Value is not { } draft)
            return null;

        return new RequirementsBasis(
            draft.Version, draft.Summary, draft.FunctionalRequirements,
            draft.AcceptanceCriteria, draft.Assumptions, draft.OpenQuestions);
    }
}

/// <summary>
/// One in-memory store wired into the real intake, approval, requirements and design services
/// (fake generators only) and the workflow service under test.
/// </summary>
public sealed class WorkflowTestFixture
{
    public InMemoryChangeRequestStore Store { get; } = new();
    public FakeSystemClock Clock { get; } = new();
    public CountingIdGenerator Ids { get; } = new();
    public FakeRequirementsGenerator RequirementsGenerator { get; } = new();
    public FakeDesignGenerator DesignGenerator { get; } = new();
    public FakeStepAvailability Availability { get; } = new();
    public ChangeRequestService Intake { get; }
    public ApprovalService Approval { get; }
    public RequirementsDraftingService Requirements { get; }
    public DesignDraftingService Designs { get; }
    public WorkflowService Workflow { get; }

    public WorkflowTestFixture()
    {
        Intake = new ChangeRequestService(Store, Clock, Ids, new ChangeRequestValidator());
        Approval = new ApprovalService(Store, Clock, Ids, new ApprovalDecisionValidator());
        Requirements = new RequirementsDraftingService(
            Store, Clock, Ids, RequirementsGenerator, new DraftContentValidator());
        Designs = new DesignDraftingService(
            Store, Clock, Ids, new TestRequirementsBasisSource(Requirements), DesignGenerator,
            new DesignContentValidator());
        Workflow = new WorkflowService(Store, Requirements, Designs, Availability);
    }

    /// <summary>Submits a valid synthetic change request and returns its id.</summary>
    public Guid SubmitSynthetic()
    {
        var result = Intake.Submit(new SubmissionRequest(
            Title: "Move nightly report schedule",
            Description: "Move the nightly synthetic report job to a quieter window.",
            Requester: "Avery Quill",
            AffectedSystem: "Sample Billing Portal",
            Priority: Priority.Medium,
            ChangeType: ChangeType.ConfigChange));
        return result.ChangeRequest!.ChangeRequestId;
    }

    /// <summary>Forces a stored request into a status that normal flows cannot reach.</summary>
    public void SeedStatus(Guid id, ChangeRequestStatus status) =>
        Store.Update(Store.FindById(id)! with { Status = status });

    /// <summary>Records a human decision through the real approval gate.</summary>
    public void Decide(Guid id, ApprovalDecision decision)
    {
        var reason = decision == ApprovalDecision.Approve ? null : "Needs more detail.";
        var result = Approval.RecordDecision(new ApprovalDecisionRequest(id, "Jordan Reed", decision, reason));
        if (!result.IsSuccess)
            throw new InvalidOperationException("Test setup could not record the decision.");
    }

    /// <summary>Submits a request and stores one requirements draft through the agent itself.</summary>
    public async Task<Guid> SubmittedWithDraftAsync()
    {
        var id = SubmitSynthetic();
        var result = await Requirements.DraftAsync(id);
        if (!result.IsSuccess)
            throw new InvalidOperationException("Test setup could not draft requirements.");
        return id;
    }

    /// <summary>Submits, drafts requirements, and approves.</summary>
    public async Task<Guid> ApprovedWithDraftAsync()
    {
        var id = await SubmittedWithDraftAsync();
        Decide(id, ApprovalDecision.Approve);
        return id;
    }

    public AuditEventType[] AuditTypes(Guid id) =>
        Store.GetAuditEntries(id).Select(e => e.EventType).ToArray();

    public int DraftCount(Guid id) => Store.GetDrafts(id).Count;

    public int DesignCount(Guid id) => Store.GetDesigns(id).Count;
}
