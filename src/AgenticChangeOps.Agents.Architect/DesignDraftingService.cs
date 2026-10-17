using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

public sealed class DesignDraftingService : IDesignDraftingService
{
    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly IRequirementsBasisSource _basisSource;
    private readonly IDesignDraftGenerator _generator;
    private readonly DesignContentValidator _validator;

    public DesignDraftingService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IRequirementsBasisSource basisSource,
        IDesignDraftGenerator generator,
        DesignContentValidator validator)
    {
        _store = store;
        _clock = clock;
        _idGenerator = idGenerator;
        _basisSource = basisSource;
        _generator = generator;
        _validator = validator;
    }

    public async Task<DesignDraftingResult> DesignAsync(
        Guid changeRequestId, CancellationToken cancellationToken = default, bool automatic = false)
    {
        var request = _store.FindById(changeRequestId);
        var (basis, eligibilityError) = CheckEligibility(changeRequestId, request);
        if (eligibilityError is not null)
            return DesignDraftingResult.Failure(new[] { eligibilityError });

        DesignContent? generated;
        try
        {
            generated = await _generator.GenerateAsync(request!, basis!, cancellationToken).ConfigureAwait(false);
        }
        catch (DraftGenerationException ex)
        {
            // The category is the only detail a generator may report.
            return DesignDraftingResult.Failure(
                new[] { new ValidationError("Generator", $"The content generator failed ({ex.Category}).") },
                ex.Category);
        }
        catch (Exception ex)
        {
            // Only the exception type is reported: messages from real clients can embed
            // endpoints or request details that must not travel in results.
            return DesignDraftingResult.Failure(new[]
            {
                new ValidationError("Generator", $"The content generator failed ({ex.GetType().Name}).")
            });
        }

        var (content, errors) = _validator.Validate(generated);
        if (errors.Count > 0)
            return DesignDraftingResult.Failure(errors, GenerationFailureCategory.MalformedOutput);

        return StoreDesign(changeRequestId, basis!.Version, content!, automatic);
    }

    public QueryResult<DesignDraft?> GetLatestDesign(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<DesignDraft?>.Failure(new[] { NotFound(changeRequestId) });

        IReadOnlyList<DesignDraft> designs;
        lock (_store.SyncRoot)
            designs = _store.GetDesigns(changeRequestId);

        return QueryResult<DesignDraft?>.Success(designs.Count == 0 ? null : designs[^1]);
    }

    public QueryResult<IReadOnlyList<DesignDraft>> GetDesignHistory(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<IReadOnlyList<DesignDraft>>.Failure(new[] { NotFound(changeRequestId) });

        // Read under the same lock as the save step so a reader never sees a design mid-store.
        lock (_store.SyncRoot)
            return QueryResult<IReadOnlyList<DesignDraft>>.Success(_store.GetDesigns(changeRequestId));
    }

    // No await in here: the eligibility re-check, version assignment and save form one
    // uninterrupted step, so overlapping calls cannot take the same version.
    private DesignDraftingResult StoreDesign(Guid changeRequestId, int basisVersion, DesignContent content, bool automatic)
    {
        lock (_store.SyncRoot)
        {
            var current = _store.FindById(changeRequestId);
            var (latest, eligibilityError) = CheckEligibility(changeRequestId, current);
            if (eligibilityError is not null)
                return DesignDraftingResult.Failure(new[] { eligibilityError });

            // The design must be linked to the requirements version the model was given.
            if (latest!.Version != basisVersion)
                return DesignDraftingResult.Failure(new[]
                {
                    new ValidationError(
                        RequirementsDraftField,
                        $"The requirements draft for change request {changeRequestId} changed while the " +
                        "design draft was being generated; request the design again.")
                });

            var generatedAt = _clock.UtcNow;
            var draft = new DesignDraft(
                DesignId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                Version: _store.GetDesigns(changeRequestId).Count + 1,
                RequirementsVersion: basisVersion,
                GeneratedAt: generatedAt,
                Status: DesignDraftStatus.Draft,
                AgentName: DesignDraft.ArchitectAgentName,
                ApproachSummary: content.ApproachSummary!,
                AffectedComponents: content.AffectedComponents!,
                ImpactAndRisks: content.ImpactAndRisks!,
                Dependencies: content.Dependencies!,
                RollbackConsiderations: content.RollbackConsiderations!,
                OpenQuestions: content.OpenQuestions!);

            var entry = new ChangeRequestAuditEntry(
                AuditEntryId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                EventType: AuditEventType.DesignGenerated,
                OccurredAt: generatedAt,
                Details: null,
                StatusBefore: current!.Status,
                StatusAfter: current.Status,
                AgentName: DesignDraft.ArchitectAgentName,
                DraftVersion: draft.Version,
                Automatic: automatic ? true : null);

            _store.SaveDesign(draft, entry);
            return DesignDraftingResult.Success(draft);
        }
    }

    private const string RequirementsDraftField = "RequirementsDraft";

    /// <summary>
    /// Order matters: existence, then status (the basis source is not consulted for a request
    /// that is not Approved), then the presence of a requirements draft.
    /// </summary>
    private (RequirementsBasis? Basis, ValidationError? Error) CheckEligibility(
        Guid changeRequestId, ChangeRequest? request)
    {
        if (request is null)
            return (null, NotFound(changeRequestId));

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

    private static ValidationError NotFound(Guid changeRequestId) =>
        new("ChangeRequestId", $"No change request with id {changeRequestId} exists.");
}
