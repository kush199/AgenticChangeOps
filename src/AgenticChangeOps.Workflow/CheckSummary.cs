using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// What one check did, as counts and a time only. <see cref="Failed"/> counts every failed step;
/// <see cref="FailuresByCategory"/> only those that failed with a category.
/// </summary>
public sealed record CheckSummary(
    DateTime CheckedAt,
    int Examined,
    int Ran,
    int Failed,
    IReadOnlyDictionary<GenerationFailureCategory, int> FailuresByCategory);
