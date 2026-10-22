namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>Operator-set ceilings: most findings, and most UTF-8 bytes of finding statements plus open questions.</summary>
public sealed record ReviewLimits(int MaxFindings, int MaxTotalBytes)
{
    public const int DefaultMaxFindings = 8;
    public const int DefaultMaxTotalBytes = 8000;

    public static ReviewLimits Defaults { get; } = new(DefaultMaxFindings, DefaultMaxTotalBytes);
}
