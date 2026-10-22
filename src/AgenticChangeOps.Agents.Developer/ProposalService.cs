using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>
/// Produces, stores and reads change proposals. The order of the steps and their guarantees are
/// the shared <see cref="DraftingEngine{TRecord, TBasis, TGenerated, TContent, TRejection}"/>'s;
/// eligibility, verification, the audit entries and the record are this service's own
/// deterministic rules; the model only supplies content. A proposal is text: nothing here
/// creates, changes, deletes, compiles or runs anything, and no status moves.
/// </summary>
public sealed class ProposalService : IProposalService
{
    private const string StatusField = "Status";
    private const string RequirementsDraftField = "RequirementsDraft";
    private const string DesignDraftField = "DesignDraft";
    private const string DesignDraftVersionField = "DesignDraftVersion";

    private readonly IChangeRequestStore _store;
    private readonly IIdGenerator _idGenerator;
    private readonly DraftingEngine<ChangeProposal, Bases, ProposalContent, ValidatedProposal, ProposalRejection> _engine;

    public ProposalService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IProposalBasisSource basisSource,
        IProposalGenerator generator,
        ProposalContentValidator validator,
        ProposalVerifier verifier)
    {
        _store = store;
        _idGenerator = idGenerator;
        _engine = new(store, clock, new Agent(store, basisSource, generator, validator, verifier));
    }

    public async Task<ProposalResult> ProposeAsync(Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var outcome = await _engine
            .RunAsync(changeRequestId, cancellationToken, Save)
            .ConfigureAwait(false);

        if (outcome.IsSuccess)
            return ProposalResult.Success(outcome.Record!);
        if (outcome.Rejection is { } rejection)
            return ProposalResult.Rejected(rejection);
        return outcome.FailureCategory is { } category
            ? ProposalResult.Failure(outcome.Errors, category)
            : ProposalResult.Failure(outcome.Errors);
    }

    public QueryResult<ChangeProposal?> GetLatestProposal(Guid changeRequestId) => _engine.GetLatest(changeRequestId);

    public QueryResult<IReadOnlyList<ChangeProposal>> GetProposalHistory(Guid changeRequestId) =>
        _engine.GetHistory(changeRequestId);

    private ChangeProposal Save(
        ChangeRequest current, Bases bases, ValidatedProposal content, int version, DateTime generatedAt)
    {
        var proposal = new ChangeProposal(
            ProposalId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            Version: version,
            RequirementsVersion: bases.Requirements.Version,
            DesignVersion: bases.Design.Version,
            GeneratedAt: generatedAt,
            Status: ProposalStatus.Draft,
            AgentName: ChangeProposal.DeveloperAgentName,
            Summary: content.Summary,
            Changes: content.Changes,
            Risks: content.Risks,
            TestIdeas: content.TestIdeas,
            OpenQuestions: content.OpenQuestions,
            SampleFilesRead: content.SampleFilesRead,
            VerificationNote: ChangeProposal.VerificationNoteText);

        // One audit entry per tool call, in call order, just before the proposal's own entry.
        // Nothing here can hold what a tool returned.
        foreach (var call in content.ToolCalls)
            _store.SaveAuditEntry(ToolCallAuditEntries.Create(
                ChangeProposal.DeveloperAgentName, _idGenerator, current.ChangeRequestId, current.Status, call, generatedAt));

        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            EventType: AuditEventType.ProposalGenerated,
            OccurredAt: generatedAt,
            Details: null,
            StatusBefore: current.Status,
            StatusAfter: current.Status,
            AgentName: ChangeProposal.DeveloperAgentName,
            DraftVersion: proposal.Version);

        _store.SaveProposal(proposal, entry);
        return proposal;
    }

    /// <summary>The requirements and design drafts a proposal is based on; both exist once eligible.</summary>
    private sealed record Bases(ProposalRequirementsBasis Requirements, ProposalDesignBasis Design);

    /// <summary>
    /// This agent's own parts. Intentional differences from the other agents: only Approved
    /// requests, a requirements draft and a design draft that is based on it must exist, the
    /// content is verified against the real sample files and the whole proposal is refused on any
    /// mismatch, it has no automatic marker, and it rethrows cancellation from the generator when
    /// the caller's own token is cancelled.
    /// </summary>
    private sealed class Agent : IDraftingAgent<ChangeProposal, Bases, ProposalContent, ValidatedProposal, ProposalRejection>
    {
        private readonly IChangeRequestStore _store;
        private readonly IProposalBasisSource _basisSource;
        private readonly IProposalGenerator _generator;
        private readonly ProposalContentValidator _validator;
        private readonly ProposalVerifier _verifier;

        public Agent(
            IChangeRequestStore store,
            IProposalBasisSource basisSource,
            IProposalGenerator generator,
            ProposalContentValidator validator,
            ProposalVerifier verifier)
        {
            _store = store;
            _basisSource = basisSource;
            _generator = generator;
            _validator = validator;
            _verifier = verifier;
        }

        public bool PropagatesCancellation => true;

        /// <summary>
        /// Order matters: existence, then status (the basis source is not consulted for a request
        /// that is not Approved), then a requirements draft, then a design draft, then that the
        /// design is based on the latest requirements draft.
        /// </summary>
        public (Bases? Basis, ValidationError? Error) CheckEligibility(Guid changeRequestId, ChangeRequest? request)
        {
            if (request is null)
                return (null, DraftingErrors.NotFound(changeRequestId));

            if (request.Status != ChangeRequestStatus.Approved)
                return (null, new ValidationError(
                    StatusField,
                    $"Change request {changeRequestId} is in status {request.Status} and cannot be given a proposal; " +
                    "only Approved requests can."));

            var basis = _basisSource.GetLatest(changeRequestId);
            if (basis.Requirements is null)
                return (null, new ValidationError(
                    RequirementsDraftField,
                    $"Change request {changeRequestId} has no requirements draft; " +
                    "a requirements draft is required before a proposal can be drafted."));

            if (basis.Design is null)
                return (null, new ValidationError(
                    DesignDraftField,
                    $"Change request {changeRequestId} has no design draft; " +
                    "a design draft is required before a proposal can be drafted."));

            if (basis.Design.RequirementsVersion != basis.Requirements.Version)
                return (null, OutdatedDesign(changeRequestId));

            return (new Bases(basis.Requirements, basis.Design), null);
        }

        public Task<ProposalContent?> GenerateAsync(
            ChangeRequest request, Bases basis, CancellationToken cancellationToken) =>
            _generator.GenerateAsync(request, basis.Requirements, basis.Design, cancellationToken);

        public ReviewOutcome<ValidatedProposal, ProposalRejection> Review(ProposalContent? generated)
        {
            var validation = _validator.Validate(generated);
            if (validation.Errors.Count > 0)
                return ReviewOutcome<ValidatedProposal, ProposalRejection>.Invalid(validation.Errors);
            if (validation.Rejection is { } shapeRejection)
                return ReviewOutcome<ValidatedProposal, ProposalRejection>.Rejected(shapeRejection);

            // The quote check: every edit is compared with the real sample files.
            if (_verifier.Verify(validation.Value!.Changes) is { } rejection)
                return ReviewOutcome<ValidatedProposal, ProposalRejection>.Rejected(rejection);

            return ReviewOutcome<ValidatedProposal, ProposalRejection>.Valid(validation.Value);
        }

        // A proposal is never stored against drafts that are no longer the latest.
        public ValidationError? CheckStillCurrent(Guid changeRequestId, Bases usedForGeneration, Bases current) =>
            current.Requirements.Version != usedForGeneration.Requirements.Version
            || current.Design.Version != usedForGeneration.Design.Version
                ? OutdatedDesign(changeRequestId)
                : null;

        public int CountExisting(Guid changeRequestId) => _store.GetProposals(changeRequestId).Count;

        public IReadOnlyList<ChangeProposal> ReadAll(Guid changeRequestId) => _store.GetProposals(changeRequestId);

        private static ValidationError OutdatedDesign(Guid changeRequestId) =>
            new(
                DesignDraftVersionField,
                $"The design draft for change request {changeRequestId} is not based on the latest requirements " +
                "draft, or a draft changed while the proposal was being generated; request a new design or try again.");
    }
}
