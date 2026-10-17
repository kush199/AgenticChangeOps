using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Automatic.Support;

/// <summary>
/// A requirements-generator behaviour that holds the step open until released, so a test can start
/// another attempt while the first is in flight.
/// </summary>
public sealed class GateProbe
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Release() => _release.TrySetResult();

    public void Install(FakeRequirementsGenerator generator) =>
        generator.Behavior = async (_, ct) =>
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return FakeRequirementsGenerator.ValidContent();
        };
}
