using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>
/// Produces, stores and reads change proposals. Eligibility, verification, versioning and the audit
/// trail are this service's own deterministic rules; the model only supplies content. A proposal is
/// text: nothing here creates, changes, deletes, compiles or runs anything, and no status moves.
/// </summary>
public sealed class ProposalService : IProposalService
{
    private const string StatusField = "Status";
    private const string RequirementsDraftField = "RequirementsDraft";
    private const string DesignDraftField = "DesignDraft";
    private const string DesignDraftVersionField = "DesignDraftVersion";
    private const int MaxAuditTargetLength = 120;

    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly IProposalBasisSource _basisSource;
    private readonly IProposalGenerator _generator;
    private readonly ProposalContentValidator _validator;
    private readonly ProposalVerifier _verifier;

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
        _clock = clock;
        _idGenerator = idGenerator;
        _basisSource = basisSource;
        _generator = generator;
        _validator = validator;
        _verifier = verifier;
    }

    public async Task<ProposalResult> ProposeAsync(Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var request = _store.FindById(changeRequestId);
        var (requirements, design, eligibilityError) = CheckEligibility(changeRequestId, request);
        if (eligibilityError is not null)
            return ProposalResult.Failure(new[] { eligibilityError });

        ProposalContent? generated;
        try
        {
            generated = await _generator
                .GenerateAsync(request!, requirements!, design!, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DraftGenerationException ex)
        {
            // The category is the only detail a generator may report.
            return ProposalResult.Failure(
                new[] { new ValidationError("Generator", $"The content generator failed ({ex.Category}).") },
                ex.Category);
        }
        catch (Exception ex)
        {
            // Only the exception type is reported: messages from real clients can embed
            // endpoints or request details that must not travel in results.
            return ProposalResult.Failure(new[]
            {
                new ValidationError("Generator", $"The content generator failed ({ex.GetType().Name}).")
            });
        }

        var validation = _validator.Validate(generated);
        if (validation.Errors.Count > 0)
            return ProposalResult.Failure(validation.Errors, GenerationFailureCategory.MalformedOutput);
        if (validation.Rejection is { } shapeRejection)
            return ProposalResult.Rejected(shapeRejection);

        if (_verifier.Verify(validation.Value!.Changes) is { } rejection)
            return ProposalResult.Rejected(rejection);

        return StoreProposal(changeRequestId, requirements!.Version, design!.Version, validation.Value);
    }

    public QueryResult<ChangeProposal?> GetLatestProposal(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<ChangeProposal?>.Failure(new[] { NotFound(changeRequestId) });

        IReadOnlyList<ChangeProposal> proposals;
        lock (_store.SyncRoot)
            proposals = _store.GetProposals(changeRequestId);

        return QueryResult<ChangeProposal?>.Success(proposals.Count == 0 ? null : proposals[^1]);
    }

    public QueryResult<IReadOnlyList<ChangeProposal>> GetProposalHistory(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<IReadOnlyList<ChangeProposal>>.Failure(new[] { NotFound(changeRequestId) });

        // Read under the same lock as the save step so a reader never sees a proposal mid-store.
        lock (_store.SyncRoot)
            return QueryResult<IReadOnlyList<ChangeProposal>>.Success(_store.GetProposals(changeRequestId));
    }

    // No await in here: the eligibility re-check, version assignment and save form one
    // uninterrupted step, so overlapping calls cannot take the same version and a proposal is
    // never stored against drafts that are no longer the latest.
    private ProposalResult StoreProposal(
        Guid changeRequestId, int requirementsVersion, int designVersion, ValidatedProposal content)
    {
        lock (_store.SyncRoot)
        {
            var current = _store.FindById(changeRequestId);
            var (requirements, design, eligibilityError) = CheckEligibility(changeRequestId, current);
            if (eligibilityError is not null)
                return ProposalResult.Failure(new[] { eligibilityError });

            if (requirements!.Version != requirementsVersion || design!.Version != designVersion)
                return ProposalResult.Failure(new[] { OutdatedDesign(changeRequestId) });

            var generatedAt = _clock.UtcNow;
            var proposal = new ChangeProposal(
                ProposalId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                Version: _store.GetProposals(changeRequestId).Count + 1,
                RequirementsVersion: requirementsVersion,
                DesignVersion: designVersion,
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
                _store.SaveAuditEntry(ToolCallEntry(changeRequestId, current!.Status, call, generatedAt));

            var entry = new ChangeRequestAuditEntry(
                AuditEntryId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                EventType: AuditEventType.ProposalGenerated,
                OccurredAt: generatedAt,
                Details: null,
                StatusBefore: current!.Status,
                StatusAfter: current.Status,
                AgentName: ChangeProposal.DeveloperAgentName,
                DraftVersion: proposal.Version);

            _store.SaveProposal(proposal, entry);
            return ProposalResult.Success(proposal);
        }
    }

    // A copy of the Architect's mapping, kept here so the Architect is not touched (plan.md,
    // Complexity Tracking).
    private ChangeRequestAuditEntry ToolCallEntry(
        Guid changeRequestId, ChangeRequestStatus status, ToolCallRecord call, DateTime generatedAt)
    {
        var target = new string(call.Target.Where(c => !char.IsControl(c)).ToArray());
        if (target.Length > MaxAuditTargetLength)
            target = target[..MaxAuditTargetLength];

        return new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: changeRequestId,
            EventType: AuditEventType.ToolCalled,
            // A call cannot have happened after the proposal it belongs to was recorded.
            OccurredAt: call.StartedAt <= generatedAt ? call.StartedAt : generatedAt,
            Details: null,
            StatusBefore: status,
            StatusAfter: status,
            AgentName: ChangeProposal.DeveloperAgentName,
            ToolName: call.ToolName,
            ToolTarget: target,
            ToolOutcome: call.Outcome.ToString(),
            ResultBytes: call.ResultBytes,
            ElapsedMilliseconds: (int)Math.Min(int.MaxValue, Math.Max(0, call.Elapsed.TotalMilliseconds)));
    }

    /// <summary>
    /// Order matters: existence, then status (the basis source is not consulted for a request that
    /// is not Approved), then a requirements draft, then a design draft, then that the design is
    /// based on the latest requirements draft.
    /// </summary>
    private (ProposalRequirementsBasis? Requirements, ProposalDesignBasis? Design, ValidationError? Error) CheckEligibility(
        Guid changeRequestId, ChangeRequest? request)
    {
        if (request is null)
            return (null, null, NotFound(changeRequestId));

        if (request.Status != ChangeRequestStatus.Approved)
            return (null, null, new ValidationError(
                StatusField,
                $"Change request {changeRequestId} is in status {request.Status} and cannot be given a proposal; " +
                "only Approved requests can."));

        var basis = _basisSource.GetLatest(changeRequestId);
        if (basis.Requirements is null)
            return (null, null, new ValidationError(
                RequirementsDraftField,
                $"Change request {changeRequestId} has no requirements draft; " +
                "a requirements draft is required before a proposal can be drafted."));

        if (basis.Design is null)
            return (null, null, new ValidationError(
                DesignDraftField,
                $"Change request {changeRequestId} has no design draft; " +
                "a design draft is required before a proposal can be drafted."));

        if (basis.Design.RequirementsVersion != basis.Requirements.Version)
            return (null, null, OutdatedDesign(changeRequestId));

        return (basis.Requirements, basis.Design, null);
    }

    private static ValidationError OutdatedDesign(Guid changeRequestId) =>
        new(
            DesignDraftVersionField,
            $"The design draft for change request {changeRequestId} is not based on the latest requirements " +
            "draft, or a draft changed while the proposal was being generated; request a new design or try again.");

    private static ValidationError NotFound(Guid changeRequestId) =>
        new("ChangeRequestId", $"No change request with id {changeRequestId} exists.");
}
