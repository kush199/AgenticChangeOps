using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// Produces, stores and reads reviews. The order of the steps and their guarantees are the shared
/// <see cref="DraftingEngine{TRecord, TBasis, TGenerated, TContent, TRejection}"/>'s; eligibility, the
/// system check, reference and limit rules, the audit entry and the record are this service's own
/// deterministic rules; the model only supplies finding text. A review is advisory content: nothing
/// here changes a status, approves, rejects or clears anything, and nothing reads a severity.
/// </summary>
public sealed class ReviewService : IReviewService
{
    private const string StatusField = "Status";
    private const string RequirementsDraftField = "RequirementsDraft";
    private const string DesignDraftField = "DesignDraft";
    private const string DesignDraftVersionField = "DesignDraftVersion";
    private const string ProposalField = "Proposal";
    private const string ProposalVersionField = "ProposalVersion";

    private readonly IIdGenerator _idGenerator;
    private readonly IChangeRequestStore _store;
    private readonly DraftingEngine<Review, Bases, GeneratedReview, ReviewToStore, ReviewRejection> _engine;

    public ReviewService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IReviewBasisSource basisSource,
        IReviewGenerator generator,
        ReviewContentValidator validator,
        SampleChecker checker)
    {
        _store = store;
        _idGenerator = idGenerator;
        _engine = new(store, clock, new Agent(store, basisSource, generator, validator, checker));
    }

    public async Task<ReviewResult> ReviewAsync(Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var outcome = await _engine
            .RunAsync(changeRequestId, cancellationToken, Save)
            .ConfigureAwait(false);

        if (outcome.IsSuccess)
            return ReviewResult.Success(outcome.Record!);
        if (outcome.Rejection is { } rejection)
            return ReviewResult.Rejected(rejection);
        return outcome.FailureCategory is { } category
            ? ReviewResult.Failure(outcome.Errors, category)
            : ReviewResult.Failure(outcome.Errors);
    }

    public QueryResult<Review?> GetLatestReview(Guid changeRequestId) => _engine.GetLatest(changeRequestId);

    public QueryResult<IReadOnlyList<Review>> GetReviewHistory(Guid changeRequestId) =>
        _engine.GetHistory(changeRequestId);

    private Review Save(ChangeRequest current, Bases bases, ReviewToStore content, int version, DateTime generatedAt)
    {
        var review = new Review(
            ReviewId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            Version: version,
            RequirementsVersion: bases.Requirements.Version,
            DesignVersion: bases.Design.Version,
            ProposalVersion: bases.Proposal.Version,
            GeneratedAt: generatedAt,
            Status: ReviewStatus.Draft,
            AgentName: Review.ReviewerAgentName,
            // The system's own result, computed before the model was asked: never anything the model wrote.
            SystemCheck: content.SystemCheck,
            Findings: content.Validated.Findings,
            OpenQuestions: content.Validated.OpenQuestions,
            AdvisoryNote: Review.AdvisoryNoteText);

        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            EventType: AuditEventType.ReviewGenerated,
            OccurredAt: generatedAt,
            Details: null,
            StatusBefore: current.Status,
            StatusAfter: current.Status,
            AgentName: Review.ReviewerAgentName,
            DraftVersion: review.Version);

        _store.SaveReview(review, entry);
        return review;
    }

    /// <summary>The requirements, design and proposal a review is based on; all exist once eligible.</summary>
    private sealed record Bases(
        ReviewRequirementsBasis Requirements, ReviewDesignBasis Design, ReviewProposalBasis Proposal);

    /// <summary>What the model produced, with the system check made before it was asked and the records it was given.</summary>
    private sealed record GeneratedReview(ReviewContent Content, SystemCheck Check, Bases Used);

    /// <summary>Content that passed every rule, with the system check that goes into the stored record.</summary>
    private sealed record ReviewToStore(ValidatedReview Validated, SystemCheck SystemCheck);

    /// <summary>
    /// This agent's own parts. Intentional differences from the other agents: only Approved requests, a
    /// requirements draft, a design draft based on it and a proposal based on that design must exist;
    /// the sample is re-checked by the system before the model is asked; every finding must cite a real
    /// entry or the whole review is refused; it has no automatic marker; and it rethrows cancellation
    /// from the generator when the caller's own token is cancelled.
    /// </summary>
    private sealed class Agent : IDraftingAgent<Review, Bases, GeneratedReview, ReviewToStore, ReviewRejection>
    {
        private readonly IChangeRequestStore _store;
        private readonly IReviewBasisSource _basisSource;
        private readonly IReviewGenerator _generator;
        private readonly ReviewContentValidator _validator;
        private readonly SampleChecker _checker;

        public Agent(
            IChangeRequestStore store,
            IReviewBasisSource basisSource,
            IReviewGenerator generator,
            ReviewContentValidator validator,
            SampleChecker checker)
        {
            _store = store;
            _basisSource = basisSource;
            _generator = generator;
            _validator = validator;
            _checker = checker;
        }

        public bool PropagatesCancellation => true;

        /// <summary>
        /// Order matters: existence, then status (the basis source is not consulted for a request that
        /// is not Approved), then a requirements draft, a design draft, that the design is based on the
        /// latest requirements, a proposal, and that the proposal is based on the latest design and
        /// requirements.
        /// </summary>
        public (Bases? Basis, ValidationError? Error) CheckEligibility(Guid changeRequestId, ChangeRequest? request)
        {
            if (request is null)
                return (null, DraftingErrors.NotFound(changeRequestId));

            if (request.Status != ChangeRequestStatus.Approved)
                return (null, new ValidationError(
                    StatusField,
                    $"Change request {changeRequestId} is in status {request.Status} and cannot be given a review; " +
                    "only Approved requests can."));

            var basis = _basisSource.GetLatest(changeRequestId);
            if (basis.Requirements is null)
                return (null, new ValidationError(
                    RequirementsDraftField,
                    $"Change request {changeRequestId} has no requirements draft; " +
                    "a requirements draft is required before a review can be drafted."));

            if (basis.Design is null)
                return (null, new ValidationError(
                    DesignDraftField,
                    $"Change request {changeRequestId} has no design draft; " +
                    "a design draft is required before a review can be drafted."));

            if (basis.Design.RequirementsVersion != basis.Requirements.Version)
                return (null, OutdatedDesign(changeRequestId));

            if (basis.Proposal is null)
                return (null, new ValidationError(
                    ProposalField,
                    $"Change request {changeRequestId} has no change proposal; " +
                    "a proposal is required before a review can be drafted."));

            if (basis.Proposal.DesignVersion != basis.Design.Version
                || basis.Proposal.RequirementsVersion != basis.Requirements.Version)
                return (null, OutdatedProposal(changeRequestId));

            return (new Bases(basis.Requirements, basis.Design, basis.Proposal), null);
        }

        public async Task<GeneratedReview?> GenerateAsync(
            ChangeRequest request, Bases basis, CancellationToken cancellationToken)
        {
            // The system's own re-check comes first, without any model, and is handed to the model as data.
            var check = _checker.Check(basis.Proposal.Changes);

            var content = await _generator
                .GenerateAsync(request, basis.Requirements, basis.Design, basis.Proposal, check, cancellationToken)
                .ConfigureAwait(false);

            return content is null ? null : new GeneratedReview(content, check, basis);
        }

        public ReviewOutcome<ReviewToStore, ReviewRejection> Review(GeneratedReview? generated)
        {
            var bases = generated is null
                ? new ReviewBases(null, null, null)
                : new ReviewBases(generated.Used.Requirements, generated.Used.Design, generated.Used.Proposal);

            var validation = _validator.Validate(generated?.Content, bases);
            if (validation.Errors.Count > 0)
                return ReviewOutcome<ReviewToStore, ReviewRejection>.Invalid(validation.Errors);
            if (validation.Rejection is { } rejection)
                return ReviewOutcome<ReviewToStore, ReviewRejection>.Rejected(rejection);

            return ReviewOutcome<ReviewToStore, ReviewRejection>.Valid(
                new ReviewToStore(validation.Value!, generated!.Check));
        }

        // A review is never stored against records that are no longer the latest.
        public ValidationError? CheckStillCurrent(Guid changeRequestId, Bases usedForGeneration, Bases current) =>
            current.Requirements.Version != usedForGeneration.Requirements.Version
            || current.Design.Version != usedForGeneration.Design.Version
            || current.Proposal.Version != usedForGeneration.Proposal.Version
                ? OutdatedProposal(changeRequestId)
                : null;

        public int CountExisting(Guid changeRequestId) => _store.GetReviews(changeRequestId).Count;

        public IReadOnlyList<Review> ReadAll(Guid changeRequestId) => _store.GetReviews(changeRequestId);

        private static ValidationError OutdatedDesign(Guid changeRequestId) =>
            new(
                DesignDraftVersionField,
                $"The design draft for change request {changeRequestId} is not based on the latest requirements " +
                "draft, or a draft changed while the review was being generated; request a new design or try again.");

        private static ValidationError OutdatedProposal(Guid changeRequestId) =>
            new(
                ProposalVersionField,
                $"The change proposal for change request {changeRequestId} is not based on the latest design and " +
                "requirements, or a record changed while the review was being generated; request a new proposal or try again.");
    }
}
