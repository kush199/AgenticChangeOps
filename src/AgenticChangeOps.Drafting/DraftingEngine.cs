using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Drafting;

/// <summary>
/// The sequence every drafting service follows, in one place: look up the change request, check
/// eligibility, call the generator, translate a generator failure into a category and nothing
/// else, review the content, then, as one uninterrupted step, re-check eligibility, check the
/// basis is still current, assign the next version and save. Also the latest and history reads.
/// </summary>
public sealed class DraftingEngine<TRecord, TBasis, TGenerated, TContent, TRejection>
    where TRecord : class
    where TBasis : class
    where TGenerated : class
    where TContent : class
    where TRejection : struct, Enum
{
    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IDraftingAgent<TRecord, TBasis, TGenerated, TContent, TRejection> _agent;

    public DraftingEngine(
        IChangeRequestStore store,
        ISystemClock clock,
        IDraftingAgent<TRecord, TBasis, TGenerated, TContent, TRejection> agent)
    {
        _store = store;
        _clock = clock;
        _agent = agent;
    }

    public async Task<DraftingOutcome<TRecord, TRejection>> RunAsync(
        Guid changeRequestId,
        CancellationToken cancellationToken,
        SaveStep<TRecord, TBasis, TContent> save)
    {
        var request = _store.FindById(changeRequestId);
        var (basis, eligibilityError) = _agent.CheckEligibility(changeRequestId, request);
        if (eligibilityError is not null)
            return DraftingOutcome<TRecord, TRejection>.Failure(new[] { eligibilityError });

        TGenerated? generated;
        try
        {
            generated = await _agent.GenerateAsync(request!, basis!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_agent.PropagatesCancellation && cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DraftGenerationException ex)
        {
            // The category is the only detail a generator may report.
            return DraftingOutcome<TRecord, TRejection>.Failure(
                new[] { new ValidationError("Generator", $"The content generator failed ({ex.Category}).") },
                ex.Category);
        }
        catch (Exception ex)
        {
            // Only the exception type is reported: messages from real clients can embed
            // endpoints or request details that must not travel in results.
            return DraftingOutcome<TRecord, TRejection>.Failure(new[]
            {
                new ValidationError("Generator", $"The content generator failed ({ex.GetType().Name}).")
            });
        }

        var review = _agent.Review(generated);
        if (review.Errors.Count > 0)
            return DraftingOutcome<TRecord, TRejection>.Failure(review.Errors, GenerationFailureCategory.MalformedOutput);
        if (review.Rejection is { } rejection)
            return DraftingOutcome<TRecord, TRejection>.Rejected(rejection);

        return Store(changeRequestId, basis!, review.Content!, save);
    }

    public QueryResult<TRecord?> GetLatest(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<TRecord?>.Failure(new[] { DraftingErrors.NotFound(changeRequestId) });

        IReadOnlyList<TRecord> records;
        lock (_store.SyncRoot)
            records = _agent.ReadAll(changeRequestId);

        return QueryResult<TRecord?>.Success(records.Count == 0 ? null : records[^1]);
    }

    public QueryResult<IReadOnlyList<TRecord>> GetHistory(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return QueryResult<IReadOnlyList<TRecord>>.Failure(new[] { DraftingErrors.NotFound(changeRequestId) });

        // Read under the same lock as the save step so a reader never sees an output mid-store.
        lock (_store.SyncRoot)
            return QueryResult<IReadOnlyList<TRecord>>.Success(_agent.ReadAll(changeRequestId));
    }

    // No await in here: the eligibility re-check, currency check, version assignment and save form
    // one uninterrupted step, so overlapping calls cannot take the same version and an output is
    // never stored against a basis that is no longer current.
    private DraftingOutcome<TRecord, TRejection> Store(
        Guid changeRequestId, TBasis usedForGeneration, TContent content, SaveStep<TRecord, TBasis, TContent> save)
    {
        lock (_store.SyncRoot)
        {
            var current = _store.FindById(changeRequestId);
            var (currentBasis, eligibilityError) = _agent.CheckEligibility(changeRequestId, current);
            if (eligibilityError is not null)
                return DraftingOutcome<TRecord, TRejection>.Failure(new[] { eligibilityError });

            if (_agent.CheckStillCurrent(changeRequestId, usedForGeneration, currentBasis!) is { } outdated)
                return DraftingOutcome<TRecord, TRejection>.Failure(new[] { outdated });

            var generatedAt = _clock.UtcNow;
            var version = _agent.CountExisting(changeRequestId) + 1;
            return DraftingOutcome<TRecord, TRejection>.Success(
                save(current!, usedForGeneration, content, version, generatedAt));
        }
    }
}
