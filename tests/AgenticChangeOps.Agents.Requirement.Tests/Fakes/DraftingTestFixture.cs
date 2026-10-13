using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Intake.Tests.Fakes;

/// <summary>
/// Wires one in-memory store, fake clock, counting id generator and fake draft generator into
/// the intake, approval and drafting services so drafting tests can seed requests and observe
/// side effects.
/// </summary>
public sealed class DraftingTestFixture
{
    public InMemoryChangeRequestStore Store { get; } = new();
    public FakeSystemClock Clock { get; } = new();
    public CountingIdGenerator Ids { get; } = new();
    public FakeDraftGenerator Generator { get; } = new();
    public ChangeRequestService Intake { get; }
    public ApprovalService Approval { get; }
    public RequirementsDraftingService Drafting { get; }

    public DraftingTestFixture()
    {
        Intake = new ChangeRequestService(Store, Clock, Ids, new ChangeRequestValidator());
        Approval = new ApprovalService(Store, Clock, Ids, new ApprovalDecisionValidator());
        Drafting = new RequirementsDraftingService(Store, Clock, Ids, Generator, new DraftContentValidator());
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
}
