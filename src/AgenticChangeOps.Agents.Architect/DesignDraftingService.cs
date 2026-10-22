using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Designs, stores and reads design drafts. The order of the steps and their guarantees are the
/// shared <see cref="DraftingEngine{TRecord, TBasis, TGenerated, TContent, TRejection}"/>'s; what
/// is specific to this agent is below: who may be designed, the requirements draft it is based on
/// and the check that it is still the latest, the content rules, and the record and audit entries
/// it stores.
/// </summary>
public sealed class DesignDraftingService : IDesignDraftingService
{
    private const string RequirementsDraftField = "RequirementsDraft";

    private readonly IChangeRequestStore _store;
    private readonly IIdGenerator _idGenerator;
    private readonly DraftingEngine<DesignDraft, RequirementsBasis, DesignContent, DesignContent, NoRejection> _engine;

    public DesignDraftingService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IRequirementsBasisSource basisSource,
        IDesignDraftGenerator generator,
        DesignContentValidator validator)
    {
        _store = store;
        _idGenerator = idGenerator;
        _engine = new(store, clock, new Agent(store, basisSource, generator, validator));
    }

    public async Task<DesignDraftingResult> DesignAsync(
        Guid changeRequestId, CancellationToken cancellationToken = default, bool automatic = false)
    {
        var outcome = await _engine
            .RunAsync(changeRequestId, cancellationToken, (current, basis, content, version, generatedAt) =>
                Save(current, basis, content, version, generatedAt, automatic))
            .ConfigureAwait(false);

        if (outcome.IsSuccess)
            return DesignDraftingResult.Success(outcome.Record!);
        return outcome.FailureCategory is { } category
            ? DesignDraftingResult.Failure(outcome.Errors, category)
            : DesignDraftingResult.Failure(outcome.Errors);
    }

    public QueryResult<DesignDraft?> GetLatestDesign(Guid changeRequestId) => _engine.GetLatest(changeRequestId);

    public QueryResult<IReadOnlyList<DesignDraft>> GetDesignHistory(Guid changeRequestId) =>
        _engine.GetHistory(changeRequestId);

    private DesignDraft Save(
        ChangeRequest current,
        RequirementsBasis basis,
        DesignContent content,
        int version,
        DateTime generatedAt,
        bool automatic)
    {
        var draft = new DesignDraft(
            DesignId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            Version: version,
            RequirementsVersion: basis.Version,
            GeneratedAt: generatedAt,
            Status: DesignDraftStatus.Draft,
            AgentName: DesignDraft.ArchitectAgentName,
            ApproachSummary: content.ApproachSummary!,
            AffectedComponents: content.AffectedComponents!,
            ImpactAndRisks: content.ImpactAndRisks!,
            Dependencies: content.Dependencies!,
            RollbackConsiderations: content.RollbackConsiderations!,
            OpenQuestions: content.OpenQuestions!,
            SampleFilesRead: content.SampleFilesRead);

        // One audit entry per tool call, in call order, just before the design's own entry.
        // Nothing here can hold what a tool returned. Without tools there are no calls and this
        // does nothing.
        foreach (var call in content.ToolCalls ?? Array.Empty<ToolCallRecord>())
            _store.SaveAuditEntry(ToolCallAuditEntries.Create(
                DesignDraft.ArchitectAgentName, _idGenerator, current.ChangeRequestId, current.Status, call, generatedAt));

        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            EventType: AuditEventType.DesignGenerated,
            OccurredAt: generatedAt,
            Details: null,
            StatusBefore: current.Status,
            StatusAfter: current.Status,
            AgentName: DesignDraft.ArchitectAgentName,
            DraftVersion: draft.Version,
            Automatic: automatic ? true : null);

        _store.SaveDesign(draft, entry);
        return draft;
    }

    /// <summary>
    /// This agent's own parts. Intentional differences from the other agents: only Approved
    /// requests are designed, a requirements draft must exist (and is the basis), the design is
    /// refused if that draft changes while it is generated, and it never rethrows cancellation
    /// from the generator.
    /// </summary>
    private sealed class Agent : IDraftingAgent<DesignDraft, RequirementsBasis, DesignContent, DesignContent, NoRejection>
    {
        private readonly IChangeRequestStore _store;
        private readonly IRequirementsBasisSource _basisSource;
        private readonly IDesignDraftGenerator _generator;
        private readonly DesignContentValidator _validator;

        public Agent(
            IChangeRequestStore store,
            IRequirementsBasisSource basisSource,
            IDesignDraftGenerator generator,
            DesignContentValidator validator)
        {
            _store = store;
            _basisSource = basisSource;
            _generator = generator;
            _validator = validator;
        }

        public bool PropagatesCancellation => false;

        /// <summary>
        /// Order matters: existence, then status (the basis source is not consulted for a request
        /// that is not Approved), then the presence of a requirements draft.
        /// </summary>
        public (RequirementsBasis? Basis, ValidationError? Error) CheckEligibility(
            Guid changeRequestId, ChangeRequest? request)
        {
            if (request is null)
                return (null, DraftingErrors.NotFound(changeRequestId));

            if (request.Status != ChangeRequestStatus.Approved)
                return (null, new ValidationError(
                    "Status",
                    $"Change request {changeRequestId} is in status {request.Status} and cannot be designed; " +
                    "only Approved requests can be designed."));

            var basis = _basisSource.GetLatest(changeRequestId);
            if (basis is null)
                return (null, new ValidationError(
                    RequirementsDraftField,
                    $"Change request {changeRequestId} has no requirements draft; " +
                    "a requirements draft is required before a design can be drafted."));

            return (basis, null);
        }

        public Task<DesignContent?> GenerateAsync(
            ChangeRequest request, RequirementsBasis basis, CancellationToken cancellationToken) =>
            _generator.GenerateAsync(request, basis, cancellationToken);

        public ReviewOutcome<DesignContent, NoRejection> Review(DesignContent? generated)
        {
            var (content, errors) = _validator.Validate(generated);
            return errors.Count > 0
                ? ReviewOutcome<DesignContent, NoRejection>.Invalid(errors)
                : ReviewOutcome<DesignContent, NoRejection>.Valid(content!);
        }

        // The design must be linked to the requirements version the model was given.
        public ValidationError? CheckStillCurrent(
            Guid changeRequestId, RequirementsBasis usedForGeneration, RequirementsBasis current) =>
            current.Version != usedForGeneration.Version
                ? new ValidationError(
                    RequirementsDraftField,
                    $"The requirements draft for change request {changeRequestId} changed while the " +
                    "design draft was being generated; request the design again.")
                : null;

        public int CountExisting(Guid changeRequestId) => _store.GetDesigns(changeRequestId).Count;

        public IReadOnlyList<DesignDraft> ReadAll(Guid changeRequestId) => _store.GetDesigns(changeRequestId);
    }
}
