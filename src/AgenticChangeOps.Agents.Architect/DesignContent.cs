namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Raw output of the content generator, before validation. A null member means the section
/// was absent; an empty list means the section was present but empty.
/// </summary>
public sealed record DesignContent(
    string? ApproachSummary,
    IReadOnlyList<string>? AffectedComponents,
    IReadOnlyList<string>? ImpactAndRisks,
    IReadOnlyList<string>? Dependencies,
    IReadOnlyList<string>? RollbackConsiderations,
    IReadOnlyList<string>? OpenQuestions,
    IReadOnlyList<string>? SampleFilesRead = null,
    IReadOnlyList<AgenticChangeOps.ModelAccess.ToolCallRecord>? ToolCalls = null);
