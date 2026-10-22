using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Requirement;

/// <summary>
/// Drafts, stores and reads requirements drafts. The order of the steps and their guarantees are
/// the shared <see cref="DraftingEngine{TRecord, TBasis, TGenerated, TContent, TRejection}"/>'s;
/// what is specific to this agent is below: who may be drafted, the content rules, and the record
/// and audit entry it stores.
/// </summary>
public sealed class RequirementsDraftingService : IRequirementsDraftingService
{
    private readonly IChangeRequestStore _store;
    private readonly IIdGenerator _idGenerator;
    private readonly DraftingEngine<RequirementsDraft, NoBasis, DraftContent, DraftContent, NoRejection> _engine;

    public RequirementsDraftingService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IRequirementsDraftGenerator generator,
        DraftContentValidator validator)
    {
        _store = store;
        _idGenerator = idGenerator;
        _engine = new(store, clock, new Agent(store, generator, validator));
    }

    public async Task<DraftingResult> DraftAsync(
        Guid changeRequestId, CancellationToken cancellationToken = default, bool automatic = false)
    {
        var outcome = await _engine
            .RunAsync(changeRequestId, cancellationToken, (current, _, content, version, generatedAt) =>
                Save(current, content, version, generatedAt, automatic))
            .ConfigureAwait(false);

        if (outcome.IsSuccess)
            return DraftingResult.Success(outcome.Record!);
        return outcome.FailureCategory is { } category
            ? DraftingResult.Failure(outcome.Errors, category)
            : DraftingResult.Failure(outcome.Errors);
    }

    public QueryResult<RequirementsDraft?> GetLatestDraft(Guid changeRequestId) => _engine.GetLatest(changeRequestId);

    public QueryResult<IReadOnlyList<RequirementsDraft>> GetDraftHistory(Guid changeRequestId) =>
        _engine.GetHistory(changeRequestId);

    private RequirementsDraft Save(
        ChangeRequest current, DraftContent content, int version, DateTime generatedAt, bool automatic)
    {
        var draft = new RequirementsDraft(
            DraftId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            Version: version,
            GeneratedAt: generatedAt,
            Status: DraftStatus.Draft,
            AgentName: RequirementsDraft.RequirementAgentName,
            Summary: content.Summary!,
            FunctionalRequirements: content.FunctionalRequirements!,
            AcceptanceCriteria: content.AcceptanceCriteria!,
            Assumptions: content.Assumptions!,
            OpenQuestions: content.OpenQuestions!);

        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            EventType: AuditEventType.DraftGenerated,
            OccurredAt: generatedAt,
            Details: null,
            StatusBefore: current.Status,
            StatusAfter: current.Status,
            AgentName: RequirementsDraft.RequirementAgentName,
            DraftVersion: draft.Version,
            Automatic: automatic ? true : null);

        _store.SaveDraft(draft, entry);
        return draft;
    }

    /// <summary>
    /// This agent's own parts. Intentional differences from the other agents: it is drafted from
    /// the change request alone (no basis, so nothing to check for currency), it accepts Submitted
    /// and Deferred requests, and it never rethrows cancellation from the generator.
    /// </summary>
    private sealed class Agent : IDraftingAgent<RequirementsDraft, NoBasis, DraftContent, DraftContent, NoRejection>
    {
        private readonly IChangeRequestStore _store;
        private readonly IRequirementsDraftGenerator _generator;
        private readonly DraftContentValidator _validator;

        public Agent(IChangeRequestStore store, IRequirementsDraftGenerator generator, DraftContentValidator validator)
        {
            _store = store;
            _generator = generator;
            _validator = validator;
        }

        public bool PropagatesCancellation => false;

        public (NoBasis? Basis, ValidationError? Error) CheckEligibility(Guid changeRequestId, ChangeRequest? request)
        {
            if (request is null)
                return (null, DraftingErrors.NotFound(changeRequestId));

            if (!IsDraftable(request.Status, hasDraft: HasDraft(changeRequestId)))
                return (null, new ValidationError(
                    "Status",
                    $"Change request {changeRequestId} is in status {request.Status} and cannot be drafted; " +
                    "only Submitted or Deferred requests can be drafted."));

            return (NoBasis.Instance, null);
        }

        public Task<DraftContent?> GenerateAsync(ChangeRequest request, NoBasis basis, CancellationToken cancellationToken) =>
            _generator.GenerateAsync(request, cancellationToken);

        public ReviewOutcome<DraftContent, NoRejection> Review(DraftContent? generated)
        {
            var (content, errors) = _validator.Validate(generated);
            return errors.Count > 0
                ? ReviewOutcome<DraftContent, NoRejection>.Invalid(errors)
                : ReviewOutcome<DraftContent, NoRejection>.Valid(content!);
        }

        public ValidationError? CheckStillCurrent(Guid changeRequestId, NoBasis usedForGeneration, NoBasis current) => null;

        public int CountExisting(Guid changeRequestId) => _store.GetDrafts(changeRequestId).Count;

        public IReadOnlyList<RequirementsDraft> ReadAll(Guid changeRequestId) => _store.GetDrafts(changeRequestId);

        // An Approved request may receive a requirements draft only while it has none, so it can
        // still reach the step that every later one depends on.
        private static bool IsDraftable(ChangeRequestStatus status, bool hasDraft) =>
            status is ChangeRequestStatus.Submitted or ChangeRequestStatus.Deferred
            || (status is ChangeRequestStatus.Approved && !hasDraft);

        private bool HasDraft(Guid changeRequestId)
        {
            lock (_store.SyncRoot)
                return _store.GetDrafts(changeRequestId).Count > 0;
        }
    }
}
