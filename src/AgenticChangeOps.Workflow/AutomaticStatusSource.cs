using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Workflow;

/// <summary>Works out the automatic mode shown in a workflow status.</summary>
public sealed class AutomaticStatusSource : IAutomaticStatusSource
{
    private readonly IChangeRequestStore _store;
    private readonly AutomaticAdvancementSettings _settings;

    public AutomaticStatusSource(IChangeRequestStore store, AutomaticAdvancementSettings settings)
    {
        _store = store;
        _settings = settings;
    }

    public AutomaticAdvancementStatus Describe(Guid changeRequestId, WorkflowStep? nextStep, int completedStepCount)
    {
        if (!_settings.Enabled)
            return new AutomaticAdvancementStatus(AutomaticMode.Off);

        if (nextStep is not { } step)
            return new AutomaticAdvancementStatus(AutomaticMode.On);

        var row = _store.GetAutomaticAttempt(changeRequestId);
        if (AutomaticAttemptRules.Applies(row, step, completedStepCount)
            && row!.ConsecutiveFailures >= _settings.MaxConsecutiveFailures)
        {
            GenerationFailureCategory? category =
                Enum.TryParse<GenerationFailureCategory>(row.LastFailureCategory, out var parsed) ? parsed : null;
            return new AutomaticAdvancementStatus(AutomaticMode.LeftForPerson, category);
        }

        return new AutomaticAdvancementStatus(AutomaticMode.On);
    }
}
