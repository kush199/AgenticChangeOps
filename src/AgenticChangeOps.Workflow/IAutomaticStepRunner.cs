namespace AgenticChangeOps.Workflow;

/// <summary>
/// Runs the next due step for the automatic path. Unlike a manual advance it does not wait for a
/// request whose step is already running: it reports <see cref="AdvanceOutcome.Busy"/>.
/// </summary>
public interface IAutomaticStepRunner
{
    Task<AdvanceResult> TryRunAsync(Guid changeRequestId, CancellationToken cancellationToken = default);
}
