using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Intake.Tests.Fakes;

/// <summary>
/// Wires one in-memory store, fake clock and counting id generator into both the intake and
/// approval services so approval tests can seed requests and observe side effects.
/// </summary>
public sealed class ApprovalTestFixture
{
    public InMemoryChangeRequestStore Store { get; } = new();
    public FakeSystemClock Clock { get; } = new();
    public CountingIdGenerator Ids { get; } = new();
    public ChangeRequestService Intake { get; }
    public ApprovalService Approval { get; }

    public ApprovalTestFixture()
    {
        Intake = new ChangeRequestService(Store, Clock, Ids, new ChangeRequestValidator());
        Approval = new ApprovalService(Store, Clock, Ids, new ApprovalDecisionValidator());
    }

    /// <summary>Submits a valid synthetic change request and returns its id.</summary>
    public Guid SubmitSynthetic()
    {
        var result = Intake.Submit(new SubmissionRequest(
            Title: "Rotate synthetic report schedule",
            Description: "Move the nightly synthetic report job to a new window.",
            Requester: "Avery Quill",
            AffectedSystem: "Sample Billing Portal",
            Priority: Priority.Medium,
            ChangeType: ChangeType.ConfigChange));
        return result.ChangeRequest!.ChangeRequestId;
    }

    public static ApprovalDecisionRequest Decide(
        Guid id,
        ApprovalDecision decision,
        string? approver = "Jordan Reed",
        string? reason = null) =>
        new(id, approver!, decision, reason);

    /// <summary>Forces a stored request into a status that normal flows cannot reach.</summary>
    public void SeedStatus(Guid id, ChangeRequestStatus status) =>
        Store.Update(Store.FindById(id)! with { Status = status });
}

/// <summary>Deterministic id generator that also reports how many ids were issued.</summary>
public sealed class CountingIdGenerator : IIdGenerator
{
    private int _issued;

    public int IssuedCount => _issued;

    public Guid NewId()
    {
        _issued++;
        return new Guid(_issued, 0, 0, new byte[] { 0xAB, 0, 0, 0, 0, 0, 0, 1 });
    }
}
