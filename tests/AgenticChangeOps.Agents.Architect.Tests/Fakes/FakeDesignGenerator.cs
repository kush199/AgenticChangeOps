using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect.Tests.Fakes;

/// <summary>
/// Stand-in content generator: never calls a model. Behaviour is configurable per test and
/// every call is counted so tests can prove the generator was (or was not) used.
/// </summary>
public sealed class FakeDesignGenerator : IDesignDraftGenerator
{
    private int _callCount;

    public int CallCount => _callCount;

    public ChangeRequest? LastRequest { get; private set; }

    public RequirementsBasis? LastBasis { get; private set; }

    public Func<ChangeRequest, RequirementsBasis, CancellationToken, Task<DesignContent?>> Behavior { get; set; } =
        (_, _, _) => Task.FromResult<DesignContent?>(ValidContent());

    public Task<DesignContent?> GenerateAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastRequest = changeRequest;
        LastBasis = basis;
        return Behavior(changeRequest, basis, cancellationToken);
    }

    public static Func<ChangeRequest, RequirementsBasis, CancellationToken, Task<DesignContent?>> Returns(
        DesignContent content) =>
        (_, _, _) => Task.FromResult<DesignContent?>(content);

    public static Func<ChangeRequest, RequirementsBasis, CancellationToken, Task<DesignContent?>> Throws(
        Exception exception) =>
        (_, _, _) => Task.FromException<DesignContent?>(exception);

    public static Func<ChangeRequest, RequirementsBasis, CancellationToken, Task<DesignContent?>> ReturnsNull() =>
        (_, _, _) => Task.FromResult<DesignContent?>(null);

    /// <summary>Complete, fictional design content for a schedule change on a sample system.</summary>
    public static DesignContent ValidContent() => new(
        ApproachSummary: "Move the nightly report job to the new window by changing its schedule entry only.",
        AffectedComponents: new[] { "Report scheduler configuration", "Nightly report job" },
        ImpactAndRisks: new[]
        {
            "Reports arrive later for early-morning readers.",
            "The new window may overlap a maintenance task."
        },
        Dependencies: new[] { "Batch import finishes before the new window opens." },
        RollbackConsiderations: new[] { "Restore the previous schedule entry from the saved configuration." },
        OpenQuestions: new[] { "Who confirms the new window with report readers?" });
}
