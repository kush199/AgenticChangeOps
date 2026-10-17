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
public sealed class WorkflowService : IWorkflowService, IAutomaticStepRunner
{
    private readonly IChangeRequestStore _store;
    private readonly IRequirementsDraftingService _requirements;
    private readonly IDesignDraftingService _designs;
    private readonly IStepAvailability _availability;
    private readonly IAutomaticStatusSource? _automatic;

    // One gate per change request so derive-then-run is a single step for that request, whether a
    // person or the automatic runner started it. Entries are never removed: the set is bounded by
    // the number of change requests.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public WorkflowService(
        IChangeRequestStore store,
        IRequirementsDraftingService requirements,
        IDesignDraftingService designs,
        IStepAvailability availability,
        IAutomaticStatusSource? automatic = null)
    {
        _store = store;
        _requirements = requirements;
        _designs = designs;
        _availability = availability;
        _automatic = automatic;
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
        var gate = Gate(changeRequestId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunNextStepAsync(changeRequestId, automatic: false, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// The automatic path. Same gate and same step as <see cref="AdvanceAsync"/>, but a request whose
    /// step is already running is skipped (<see cref="AdvanceOutcome.Busy"/>) instead of waited for.
    /// </summary>
    public async Task<AdvanceResult> TryRunAsync(
        Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var gate = Gate(changeRequestId);
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            var current = Derive(changeRequestId);
            return current is null
                ? AdvanceResult.NotFound(new[] { NotFound(changeRequestId) })
                : AdvanceResult.Busy(current);
        }

        try
        {
            return await RunNextStepAsync(changeRequestId, automatic: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim Gate(Guid changeRequestId) =>
        _gates.GetOrAdd(changeRequestId, _ => new SemaphoreSlim(1, 1));

    /// <summary>Runs inside the request's gate: re-derives, then runs at most one step.</summary>
    private async Task<AdvanceResult> RunNextStepAsync(
        Guid changeRequestId, bool automatic, CancellationToken cancellationToken)
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
            var result = await _requirements.DraftAsync(changeRequestId, cancellationToken, automatic)
                .ConfigureAwait(false);
            var after = Derive(changeRequestId)!;
            return result.IsSuccess
                ? AdvanceResult.Advanced(result, after)
                : AdvanceResult.StepFailed(result, after);
        }
        else
        {
            var result = await _designs.DesignAsync(changeRequestId, cancellationToken, automatic)
                .ConfigureAwait(false);
            var after = Derive(changeRequestId)!;
            return result.IsSuccess
                ? AdvanceResult.Advanced(result, after)
                : AdvanceResult.StepFailed(result, after);
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

        var status = WorkflowRules.Derive(
            changeRequestId,
            request.Status,
            drafts.Select(d => new RequirementsFact(d.Version, d.GeneratedAt)).ToArray(),
            designs.Select(d => new DesignFact(d.Version, d.GeneratedAt, d.RequirementsVersion)).ToArray());

        // The automatic block is derived from settings and attempt rows only; it never changes the
        // stage, the next step or the reason, which come from the rules alone.
        return _automatic is null
            ? status
            : status with { Automatic = _automatic.Describe(changeRequestId, status.NextStep, status.CompletedSteps.Count) };
    }

    private static ValidationError NotFound(Guid changeRequestId) =>
        new("ChangeRequestId", $"No change request with id {changeRequestId} exists.");
}
