namespace AgenticChangeOps.Workflow;

/// <summary>A stored requirements draft, reduced to what the rules need.</summary>
public sealed record RequirementsFact(int Version, DateTime GeneratedAt);

/// <summary>A stored design draft, reduced to what the rules need.</summary>
public sealed record DesignFact(int Version, DateTime GeneratedAt, int RequirementsVersion);
