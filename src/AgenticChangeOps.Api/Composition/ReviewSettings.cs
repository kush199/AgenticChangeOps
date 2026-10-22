using System.Globalization;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Operator settings for the Reviewer agent: the most findings a review may hold and the most bytes
/// of finding text. Read only when the review service is first used, so a bad value cannot affect a
/// server that never uses the Reviewer. A present value that is not a positive whole number is
/// invalid and reported by setting name only.
/// </summary>
public sealed record ReviewSettings(ReviewLimits Limits)
{
    public const string MaxFindingsVariable = "AGENTICCHANGEOPS_REVIEW_MAX_FINDINGS";
    public const string MaxBytesVariable = "AGENTICCHANGEOPS_REVIEW_MAX_BYTES";

    /// <summary>
    /// Throws <see cref="GeneratorConfigurationException"/> naming the first setting that is wrong;
    /// the supplied value is never put in the message.
    /// </summary>
    public static ReviewSettings FromConfiguration(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        return new ReviewSettings(new ReviewLimits(
            ReadPositive(getVariable, MaxFindingsVariable, ReviewLimits.DefaultMaxFindings),
            ReadPositive(getVariable, MaxBytesVariable, ReviewLimits.DefaultMaxTotalBytes)));
    }

    private static int ReadPositive(Func<string, string?> getVariable, string name, int defaultValue)
    {
        var raw = getVariable(name);
        if (raw is null)
            return defaultValue;

        if (!int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new GeneratorConfigurationException(name, "it must be a positive whole number");

        return value;
    }
}
