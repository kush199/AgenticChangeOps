using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

/// <summary>
/// Stand-in content generator: never calls a model. Behaviour is configurable per test, every call
/// is counted, and it remembers exactly what it was given, including the system check, so tests can
/// prove the check was computed before the model was asked.
/// </summary>
public sealed class FakeReviewGenerator : IReviewGenerator
{
    private int _callCount;

    public int CallCount => _callCount;

    public ChangeRequest? LastRequest { get; private set; }
    public ReviewRequirementsBasis? LastRequirements { get; private set; }
    public ReviewDesignBasis? LastDesign { get; private set; }
    public ReviewProposalBasis? LastProposal { get; private set; }
    public SystemCheck? LastCheck { get; private set; }

    public Func<CancellationToken, Task<ReviewContent?>> Behavior { get; set; } =
        _ => Task.FromResult<ReviewContent?>(ValidContent());

    public Task<ReviewContent?> GenerateAsync(
        ChangeRequest changeRequest,
        ReviewRequirementsBasis requirements,
        ReviewDesignBasis design,
        ReviewProposalBasis proposal,
        SystemCheck systemCheck,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastRequest = changeRequest;
        LastRequirements = requirements;
        LastDesign = design;
        LastProposal = proposal;
        LastCheck = systemCheck;
        return Behavior(cancellationToken);
    }

    public static Func<CancellationToken, Task<ReviewContent?>> Returns(ReviewContent content) =>
        _ => Task.FromResult<ReviewContent?>(content);

    public static Func<CancellationToken, Task<ReviewContent?>> Throws(Exception exception) =>
        _ => Task.FromException<ReviewContent?>(exception);

    public static Func<CancellationToken, Task<ReviewContent?>> ReturnsNull() =>
        _ => Task.FromResult<ReviewContent?>(null);

    public const string FirstStatement = "No test covers the old hour.";
    public const string SecondStatement = "The notes file may drift from the settings.";

    public static ReviewFindingContent Finding(
        string statement = FirstStatement,
        string severity = "Medium",
        params string[] references) =>
        new("AcceptanceCriteria", severity, statement, references.Length == 0 ? new[] { "proposal.changes[1]" } : references);

    /// <summary>Two findings whose references all resolve against <see cref="FakeReviewBasisSource.Current"/>.</summary>
    public static ReviewContent ValidContent() => new(
        Findings: new[]
        {
            new ReviewFindingContent("AcceptanceCriteria", "Medium", FirstStatement,
                new[] { "requirements.acceptanceCriteria[2]", "proposal.testIdeas[1]" }),
            new ReviewFindingContent("UnmentionedRisk", "Low", SecondStatement,
                new[] { "proposal.risks[2]" })
        },
        OpenQuestions: new[] { "Who owns the notes file?" });
}
