namespace AgenticChangeOps.Workflow;

/// <summary>A stored requirements draft, reduced to what the rules need.</summary>
public sealed record RequirementsFact(int Version, DateTime GeneratedAt);

/// <summary>A stored design draft, reduced to what the rules need.</summary>
public sealed record DesignFact(int Version, DateTime GeneratedAt, int RequirementsVersion);

/// <summary>A stored change proposal, reduced to what the offer rule needs.</summary>
public sealed record ProposalFact(int Version, int DesignVersion);

/// <summary>A stored review, reduced to the version of the proposal it is based on.</summary>
public sealed record ReviewFact(int ProposalVersion);
