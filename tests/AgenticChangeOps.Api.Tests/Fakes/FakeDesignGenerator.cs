using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Stand-in design generator: no model, no network. Counts calls.</summary>
public sealed class FakeDesignGenerator : IDesignDraftGenerator
{
    private readonly Func<ChangeRequest, RequirementsBasis, DesignContent?> _behavior;
    private int _callCount;

    private FakeDesignGenerator(Func<ChangeRequest, RequirementsBasis, DesignContent?> behavior)
    {
        _behavior = behavior;
    }

    public int CallCount => _callCount;

    public static DesignContent SyntheticContent() => new(
        ApproachSummary: "Shorten the sample catalogue cache lifetime through configuration only.",
        AffectedComponents: new[] { "Sample catalogue cache settings" },
        ImpactAndRisks: new[] { "Briefly higher load on the catalogue store after the change." },
        Dependencies: new[] { "Catalogue store capacity" },
        RollbackConsiderations: new[] { "Restore the previous lifetime value." },
        OpenQuestions: new[] { "Should the lifetime differ per environment?" });

    public static FakeDesignGenerator ReturnsSyntheticContent() => new((_, _) => SyntheticContent());

    public static FakeDesignGenerator ReturnsContent(DesignContent? content) => new((_, _) => content);

    public static FakeDesignGenerator ThrowsCategory(GenerationFailureCategory category) =>
        new((_, _) => throw new DraftGenerationException(category));

    public static FakeDesignGenerator ThrowsArbitrary(Exception exception) =>
        new((_, _) => throw exception);

    public Task<DesignContent?> GenerateAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        try
        {
            return Task.FromResult(_behavior(changeRequest, basis));
        }
        catch (Exception ex)
        {
            return Task.FromException<DesignContent?>(ex);
        }
    }
}
