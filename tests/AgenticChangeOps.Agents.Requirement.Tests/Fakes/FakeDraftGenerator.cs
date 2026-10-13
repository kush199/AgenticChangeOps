using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Agents.Requirement.Tests.Fakes;

/// <summary>
/// Stand-in content generator: never calls a model. Behaviour is configurable per test and
/// every call is counted so tests can prove the generator was (or was not) used.
/// </summary>
public sealed class FakeDraftGenerator : IRequirementsDraftGenerator
{
    private int _callCount;

    public int CallCount => _callCount;

    public ChangeRequest? LastRequest { get; private set; }

    public Func<ChangeRequest, CancellationToken, Task<DraftContent?>> Behavior { get; set; } =
        (_, _) => Task.FromResult<DraftContent?>(ValidContent());

    public Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastRequest = changeRequest;
        return Behavior(changeRequest, cancellationToken);
    }

    public static Func<ChangeRequest, CancellationToken, Task<DraftContent?>> Returns(DraftContent content) =>
        (_, _) => Task.FromResult<DraftContent?>(content);

    public static Func<ChangeRequest, CancellationToken, Task<DraftContent?>> Throws(Exception exception) =>
        (_, _) => Task.FromException<DraftContent?>(exception);

    public static Func<ChangeRequest, CancellationToken, Task<DraftContent?>> ReturnsNull() =>
        (_, _) => Task.FromResult<DraftContent?>(null);

    /// <summary>Complete, fictional content about a schedule change on a sample system.</summary>
    public static DraftContent ValidContent() => new(
        Summary: "Move the nightly Sample Billing Portal report job to a quieter window.",
        FunctionalRequirements: new[]
        {
            "The report job runs at the newly configured time each night.",
            "The previous schedule entry is removed once the new one is active."
        },
        AcceptanceCriteria: new[]
        {
            "The nightly report is produced within the new window on three consecutive nights.",
            "No report is produced at the old time."
        },
        Assumptions: new[] { "The new window has no competing batch jobs." },
        OpenQuestions: new[] { "Should weekend runs keep the old schedule?" });
}
