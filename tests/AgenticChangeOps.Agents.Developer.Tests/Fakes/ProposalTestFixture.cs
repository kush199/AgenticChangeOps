using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;

namespace AgenticChangeOps.Agents.Developer.Tests.Fakes;

/// <summary>
/// Wires one in-memory store, fake clock, counting id generator, fake basis source, fake sample
/// and fake generator into the intake, approval and proposal services so tests can seed requests
/// and observe side effects.
/// </summary>
public sealed class ProposalTestFixture
{
    public InMemoryChangeRequestStore Store { get; } = new();
    public FakeSystemClock Clock { get; } = new();
    public CountingIdGenerator Ids { get; } = new();
    public FakeProposalGenerator Generator { get; } = new();
    public FakeProposalBasisSource Basis { get; } = new();
    public FakeSampleFiles Sample { get; } = FakeSampleFiles.Standard();
    public ChangeRequestService Intake { get; }
    public ApprovalService Approval { get; }
    public ProposalService Proposing { get; }

    public ProposalTestFixture(ProposalLimits? limits = null)
    {
        Intake = new ChangeRequestService(Store, Clock, Ids, new ChangeRequestValidator());
        Approval = new ApprovalService(Store, Clock, Ids, new ApprovalDecisionValidator());
        Proposing = new ProposalService(
            Store, Clock, Ids, Basis, Generator,
            new ProposalContentValidator(),
            new ProposalVerifier(Sample, limits ?? ProposalLimits.Defaults));
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

    /// <summary>
    /// Submits a request, gives it current requirements and design drafts, and approves it through
    /// the real approval gate.
    /// </summary>
    public Guid ApproveWithDrafts(int requirementsVersion = 1, int designVersion = 1)
    {
        var id = SubmitSynthetic();
        Basis.Set(id, FakeProposalBasisSource.Current(requirementsVersion, designVersion));
        var decision = Approval.RecordDecision(
            new ApprovalDecisionRequest(id, "Jordan Reed", ApprovalDecision.Approve, null));
        if (!decision.IsSuccess)
            throw new InvalidOperationException("Test setup could not approve the request.");
        return id;
    }

    /// <summary>Counts the audit entries of one type for a change request.</summary>
    public int AuditCount(Guid id, AuditEventType type) =>
        Store.GetAuditEntries(id).Count(e => e.EventType == type);
}
