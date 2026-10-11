using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Intake.Services;

public sealed class RequirementsDraftingService : IRequirementsDraftingService
{
    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly IRequirementsDraftGenerator _generator;
    private readonly DraftContentValidator _validator;
    private readonly object _gate = new();

    public RequirementsDraftingService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        IRequirementsDraftGenerator generator,
        DraftContentValidator validator)
    {
        _store = store;
        _clock = clock;
        _idGenerator = idGenerator;
        _generator = generator;
        _validator = validator;
    }

    public async Task<DraftingResult> DraftAsync(Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var request = _store.FindById(changeRequestId);
        var eligibilityError = CheckEligibility(changeRequestId, request);
        if (eligibilityError is not null)
            return DraftingResult.Failure(new[] { eligibilityError });

        DraftContent? generated;
        try
        {
            generated = await _generator.GenerateAsync(request!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Only the exception type is reported: messages from real clients can embed
            // endpoints or request details that must not travel in results.
            return DraftingResult.Failure(new[]
            {
                new ValidationError("Generator", $"The content generator failed ({ex.GetType().Name}).")
            });
        }

        var (content, errors) = _validator.Validate(generated);
        if (errors.Count > 0)
            return DraftingResult.Failure(errors);

        return StoreDraft(changeRequestId, content!);
    }

    public QueryResult<RequirementsDraft?> GetLatestDraft(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<RequirementsDraft?>.Failure(new[] { NotFound(changeRequestId) });

        IReadOnlyList<RequirementsDraft> drafts;
        lock (_gate)
            drafts = _store.GetDrafts(changeRequestId);

        return QueryResult<RequirementsDraft?>.Success(drafts.Count == 0 ? null : drafts[^1]);
    }

    public QueryResult<IReadOnlyList<RequirementsDraft>> GetDraftHistory(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<IReadOnlyList<RequirementsDraft>>.Failure(new[] { NotFound(changeRequestId) });

        // Read under the same lock as the save step so a reader never sees a draft mid-store.
        lock (_gate)
            return QueryResult<IReadOnlyList<RequirementsDraft>>.Success(_store.GetDrafts(changeRequestId));
    }

    // No await in here: the eligibility re-check, version assignment and save form one
    // uninterrupted step, so overlapping calls cannot take the same version.
    private DraftingResult StoreDraft(Guid changeRequestId, DraftContent content)
    {
        lock (_gate)
        {
            var current = _store.FindById(changeRequestId);
            var eligibilityError = CheckEligibility(changeRequestId, current);
            if (eligibilityError is not null)
                return DraftingResult.Failure(new[] { eligibilityError });

            var generatedAt = _clock.UtcNow;
            var draft = new RequirementsDraft(
                DraftId: _idGenerator.NewId(),
                ChangeRequestId: changeRequestId,
                Version: _store.GetDrafts(changeRequestId).Count + 1,
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
                ChangeRequestId: changeRequestId,
                EventType: AuditEventType.DraftGenerated,
                OccurredAt: generatedAt,
                Details: null,
                StatusBefore: current!.Status,
                StatusAfter: current.Status,
                AgentName: RequirementsDraft.RequirementAgentName,
                DraftVersion: draft.Version);

            _store.SaveDraft(draft, entry);
            return DraftingResult.Success(draft);
        }
    }

    private static ValidationError? CheckEligibility(Guid changeRequestId, ChangeRequest? request)
    {
        if (request is null)
            return NotFound(changeRequestId);

        if (!IsDraftable(request.Status))
            return new ValidationError(
                "Status",
                $"Change request {changeRequestId} is in status {request.Status} and cannot be drafted; " +
                "only Submitted or Deferred requests can be drafted.");

        return null;
    }

    private static bool IsDraftable(ChangeRequestStatus status) =>
        status is ChangeRequestStatus.Submitted or ChangeRequestStatus.Deferred;

    private static ValidationError NotFound(Guid changeRequestId) =>
        new("ChangeRequestId", $"No change request with id {changeRequestId} exists.");
}
