using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Runs the automatic-advancement check on a timer. Does nothing at all when switched off: no timer
/// is created and no check runs. A check runs at start (so a restart catches up from the recorded
/// facts) and then once per interval; the next tick is awaited only after a check returns, so checks
/// never overlap.
/// </summary>
public sealed class AutomaticAdvancementHostedService : BackgroundService
{
    private readonly AutomaticAdvancementService _service;
    private readonly AutomaticAdvancementSettings _settings;
    private readonly CheckSummaryHolder _holder;
    private readonly ILogger<AutomaticAdvancementHostedService> _logger;

    public AutomaticAdvancementHostedService(
        AutomaticAdvancementService service,
        AutomaticAdvancementSettings settings,
        CheckSummaryHolder holder,
        ILogger<AutomaticAdvancementHostedService> logger)
    {
        _service = service;
        _settings = settings;
        _holder = holder;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
            return;

        // Let the host finish starting before the first check.
        await Task.Yield();

        using var timer = new PeriodicTimer(_settings.Interval);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: a step in progress was cancelled and stored nothing.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _service.RunCheckAsync(cancellationToken).ConfigureAwait(false);
            _holder.Record(summary);

            var categories = summary.FailuresByCategory.Count == 0
                ? string.Empty
                : " categories=" + string.Join(",", summary.FailuresByCategory
                    .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}:{pair.Value}"));
            _logger.LogInformation(
                "Automatic advancement check: examined={Examined} ran={Ran} failed={Failed}{Categories}",
                summary.Examined, summary.Ran, summary.Failed, categories);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Only the type name is logged: messages can carry request or endpoint detail.
            _logger.LogWarning("Automatic advancement check failed ({ExceptionType}).", ex.GetType().Name);
        }
    }
}
