using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Records a person's Accept or Reject on the latest design version. Never automated: nothing but the
/// decision route calls it. The checks and the write run as one uninterrupted step under the store's
/// lock, and the store refuses a second decision of the same version, so overlapping decisions store
/// exactly one. A decision never updates the change request.
/// </summary>
public sealed class DesignDecisionService : IDesignDecisionService
{
    private const string DesignVersionField = "DesignVersion";

    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly IRequirementsBasisSource _basisSource;
    private readonly DesignDecisionValidator _validator = new();

    public DesignDecisionService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IRequirementsBasisSource basisSource)
    {
        _store = store;
        _clock = clock;
        _idGenerator = idGenerator;
        _basisSource = basisSource;
    }

    public DesignDecisionResult Decide(
        Guid changeRequestId, int version, string approverName, DesignVerdict verdict, string? reason)
    {
        lock (_store.SyncRoot)
        {
            var request = _store.FindById(changeRequestId);
            if (request is null)
                return Refuse(DesignDecisionRefusal.NotFound, DraftingErrors.NotFound(changeRequestId));

            if (request.Status != ChangeRequestStatus.Approved)
                return Refuse(DesignDecisionRefusal.NotApproved, new ValidationError(
                    "Status",
                    $"Change request {changeRequestId} is in status {request.Status} and its design cannot " +
                    "be decided; only Approved requests can."));

            var designs = _store.GetDesigns(changeRequestId);
            var design = designs.FirstOrDefault(d => d.Version == version);
            if (design is null)
                return Refuse(DesignDecisionRefusal.VersionNotFound, new ValidationError(
                    DesignVersionField, $"Change request {changeRequestId} has no design version {version}."));

            var latestRequirements = _basisSource.GetLatest(changeRequestId)?.Version;
            if (design.Version != designs.Max(d => d.Version) || design.RequirementsVersion != latestRequirements)
                return Refuse(DesignDecisionRefusal.Outdated, new ValidationError(
                    DesignVersionField,
                    $"Design version {version} is outdated; only the latest design, based on the latest " +
                    "requirements draft, can be decided."));

            if (_store.GetAgentOutputs<DesignDecision>(changeRequestId).Any(d => d.Version == version))
                return Refuse(DesignDecisionRefusal.AlreadyDecided, AlreadyDecided(version));

            var errors = _validator.Validate(approverName, verdict, reason);
            if (errors.Count > 0)
                return DesignDecisionResult.Failure(DesignDecisionRefusal.Invalid, errors);

            var decidedAt = _clock.UtcNow;
            var trimmedReason = reason?.Trim();
            var decision = new DesignDecision(
                DecisionId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                Version: version,
                DesignId: design.DesignId,
                Outcome: verdict == DesignVerdict.Accept ? DesignDecisionOutcome.Accepted : DesignDecisionOutcome.Rejected,
                ApproverName: approverName.Trim(),
                Reason: string.IsNullOrEmpty(trimmedReason) ? null : trimmedReason,
                DecidedAt: decidedAt);

            var entry = new ChangeRequestAuditEntry(
                AuditEntryId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                EventType: AuditEventType.DesignDecided,
                OccurredAt: decidedAt,
                Details: decision.Outcome.ToString(),
                ApproverName: decision.ApproverName,
                Reason: decision.Reason,
                StatusBefore: request.Status,
                StatusAfter: request.Status,
                DraftVersion: version);

            try
            {
                _store.SaveAgentOutput(decision, entry);
            }
            catch (StorageVersionConflictException)
            {
                return Refuse(DesignDecisionRefusal.AlreadyDecided, AlreadyDecided(version));
            }

            return DesignDecisionResult.Success(decision);
        }
    }

    public IReadOnlyList<DesignDecision> GetDecisions(Guid changeRequestId) =>
        _store.GetAgentOutputs<DesignDecision>(changeRequestId);

    private static ValidationError AlreadyDecided(int version) =>
        new(DesignVersionField, $"Design version {version} has already been decided; the decision is final.");

    private static DesignDecisionResult Refuse(DesignDecisionRefusal refusal, ValidationError error) =>
        DesignDecisionResult.Failure(refusal, new[] { error });
}
