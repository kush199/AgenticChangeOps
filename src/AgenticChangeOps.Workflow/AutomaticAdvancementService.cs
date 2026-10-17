using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// One check of automatic advancement: finds requests with a step due by the same rules the
/// workflow status uses and runs them through the same path as a manual advance. Never asks a model
/// what to run; the only writes it makes are the failure bookkeeping rows.
/// </summary>
public sealed class AutomaticAdvancementService
{
    private readonly IChangeRequestStore _store;
    private readonly IWorkflowService _workflow;
    private readonly IAutomaticStepRunner _runner;
    private readonly ISystemClock _clock;
    private readonly AutomaticAdvancementSettings _settings;
    private readonly IStepAvailability _availability;

    public AutomaticAdvancementService(
        IChangeRequestStore store,
        IWorkflowService workflow,
        IAutomaticStepRunner runner,
        ISystemClock clock,
        AutomaticAdvancementSettings settings,
        IStepAvailability availability)
    {
        _store = store;
        _workflow = workflow;
        _runner = runner;
        _clock = clock;
        _settings = settings;
        _availability = availability;
    }

    public async Task<CheckSummary> RunCheckAsync(CancellationToken cancellationToken = default)
    {
        var examined = 0;
        var ran = 0;
        var failed = 0;
        var budgetUsed = 0;
        var byCategory = new Dictionary<GenerationFailureCategory, int>();

        // Requests are handled in the order they were stored. A request that succeeds stops being
        // due and one that fails is skipped until its wait passes, so the head of the queue always
        // moves and later requests are not starved.
        foreach (var id in _store.ListChangeRequestIds())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budgetUsed >= _settings.MaxStepsPerCheck)
                break;

            examined++;
            try
            {
                var attempt = await ExamineAsync(id, cancellationToken).ConfigureAwait(false);
                if (attempt.Used)
                    budgetUsed++;
                if (attempt.Ran)
                    ran++;
                if (attempt.Failed)
                {
                    failed++;
                    if (attempt.Category is { } category)
                        byCategory[category] = byCategory.GetValueOrDefault(category) + 1;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Only the fact of a failure is kept: no row can be trusted to be written, and the
                // exception text is never stored or logged.
                failed++;
                budgetUsed++;
            }
        }

        return new CheckSummary(_clock.UtcNow, examined, ran, failed, byCategory);
    }

    private async Task<Examination> ExamineAsync(Guid id, CancellationToken cancellationToken)
    {
        var query = _workflow.GetStatus(id);
        if (!query.IsSuccess || query.Value!.NextStep is not { } step)
            return Examination.Nothing;

        var status = query.Value;
        if (!_availability.IsConfigured(step))
            return Examination.Nothing;

        var row = _store.GetAutomaticAttempt(id);
        var applies = AutomaticAttemptRules.Applies(row, step, status.CompletedSteps.Count);
        if (applies)
        {
            if (row!.ConsecutiveFailures >= _settings.MaxConsecutiveFailures)
                return Examination.Nothing;
            if (_clock.UtcNow < row.LastFailureAt + _settings.FailureWait)
                return Examination.Nothing;
        }

        var result = await _runner.TryRunAsync(id, cancellationToken).ConfigureAwait(false);
        switch (result.Outcome)
        {
            case AdvanceOutcome.Advanced:
                if (row is not null)
                    _store.ClearAutomaticAttempt(id);
                return new Examination(Used: true, Ran: true, Failed: false, Category: null);

            case AdvanceOutcome.StepFailed:
                var category = result.Requirements?.FailureCategory ?? result.Design?.FailureCategory;
                if (category is not { } failure)
                {
                    // The facts changed while the model ran (for example a decision was recorded):
                    // nothing to count, the next check derives again.
                    return new Examination(Used: true, Ran: false, Failed: false, Category: null);
                }

                _store.SaveAutomaticAttempt(new AutomaticAttempt(
                    ChangeRequestId: id,
                    Step: step.ToString(),
                    CompletedStepCount: status.CompletedSteps.Count,
                    ConsecutiveFailures: applies ? row!.ConsecutiveFailures + 1 : 1,
                    LastFailureCategory: failure.ToString(),
                    LastFailureAt: _clock.UtcNow));
                return new Examination(Used: true, Ran: false, Failed: true, Category: failure);

            default:
                return Examination.Nothing;
        }
    }

    private readonly record struct Examination(bool Used, bool Ran, bool Failed, GenerationFailureCategory? Category)
    {
        public static Examination Nothing => new(false, false, false, null);
    }
}
