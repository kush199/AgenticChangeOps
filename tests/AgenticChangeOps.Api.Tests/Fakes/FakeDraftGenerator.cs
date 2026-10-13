using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Stand-in generator: no model, no network. Counts calls.</summary>
public sealed class FakeDraftGenerator : IRequirementsDraftGenerator
{
    private readonly Func<ChangeRequest, DraftContent?> _behavior;

    private FakeDraftGenerator(Func<ChangeRequest, DraftContent?> behavior)
    {
        _behavior = behavior;
    }

    public int CallCount { get; private set; }

    public static FakeDraftGenerator ReturnsSyntheticContent() => new(_ => new DraftContent(
        "Adjust the sample catalogue cache lifetime.",
        new[] { "The catalogue cache lifetime is configurable." },
        new[] { "Changing the lifetime takes effect without a restart." },
        new[] { "The sample catalogue is the only affected system." },
        new[] { "Should the lifetime differ per environment?" }));

    public static FakeDraftGenerator ReturnsContent(DraftContent? content) => new(_ => content);

    public static FakeDraftGenerator ThrowsCategory(GenerationFailureCategory category) =>
        new(_ => throw new DraftGenerationException(category));

    public static FakeDraftGenerator ThrowsArbitrary(Exception exception) =>
        new(_ => throw exception);

    public Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_behavior(changeRequest));
    }
}
