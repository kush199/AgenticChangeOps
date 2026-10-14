using System.Collections.Concurrent;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// Reports and advances the workflow. Reads recorded facts and calls the agents' own services;
/// it never builds a draft, writes an audit entry or records a decision itself.
/// </summary>
public sealed class WorkflowService : IWorkflowService
{
    private readonly IChangeRequestStore _store;
    private readonly IRequirementsDraftingService _requirements;
    private readonly IDesignDraftingService _designs;
    private readonly IStepAvailability _availability;

    // One gate per change request so derive-then-run is a single step for that request. Entries
    // are never removed: the store is in-memory, so the set is bounded by its own size.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public WorkflowService(
        IChangeRequestStore store,
        IRequirementsDraftingService requirements,
        IDesignDraftingService designs,
        IStepAvailability availability)
    {
        _store = store;
        _requirements = requirements;
        _designs = designs;
        _availability = availability;
    }

    public QueryResult<WorkflowStatus> GetStatus(Guid changeRequestId)
    {
        var status = Derive(changeRequestId);
        return status is null
            ? QueryResult<WorkflowStatus>.Failure(new[] { NotFound(changeRequestId) })
            : QueryResult<WorkflowStatus>.Success(status);
    }

    public async Task<AdvanceResult> AdvanceAsync(
        Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(changeRequestId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-derived inside the gate: a status read earlier is never acted on.
            var status = Derive(changeRequestId);
            if (status is null)
                return AdvanceResult.NotFound(new[] { NotFound(changeRequestId) });

            if (status.NextStep is not { } step)
                return AdvanceResult.NothingDue(status);

            if (!_availability.IsConfigured(step))
                return AdvanceResult.NotConfigured(step, status);

            if (step == WorkflowStep.RequirementsDraft)
            {
                var result = await _requirements.DraftAsync(changeRequestId, cancellationToken).ConfigureAwait(false);
                var after = Derive(changeRequestId)!;
                return result.IsSuccess
                    ? AdvanceResult.Advanced(result, after)
                    : AdvanceResult.StepFailed(result, after);
            }
            else
            {
                var result = await _designs.DesignAsync(changeRequestId, cancellationToken).ConfigureAwait(false);
                var after = Derive(changeRequestId)!;
                return result.IsSuccess
                    ? AdvanceResult.Advanced(result, after)
                    : AdvanceResult.StepFailed(result, after);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Reads the facts in one step under the store lock and applies the rules.</summary>
    private WorkflowStatus? Derive(Guid changeRequestId)
    {
        ChangeRequest? request;
        IReadOnlyList<RequirementsDraft> drafts;
        IReadOnlyList<DesignDraft> designs;

        lock (_store.SyncRoot)
        {
            request = _store.FindById(changeRequestId);
            if (request is null)
                return null;

            drafts = _store.GetDrafts(changeRequestId);
            designs = _store.GetDesigns(changeRequestId);
        }

        return WorkflowRules.Derive(
            changeRequestId,
            request.Status,
            drafts.Select(d => new RequirementsFact(d.Version, d.GeneratedAt)).ToArray(),
            designs.Select(d => new DesignFact(d.Version, d.GeneratedAt, d.RequirementsVersion)).ToArray());
    }

    private static ValidationError NotFound(Guid changeRequestId) =>
        new("ChangeRequestId", $"No change request with id {changeRequestId} exists.");
}
