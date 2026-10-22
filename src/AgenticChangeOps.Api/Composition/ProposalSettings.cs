using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Operator settings for the Developer agent's proposal ceilings. Read only when the sample tools
/// are switched on, so a bad value cannot affect a server that has the Developer unused. A present
/// value that is not a positive whole number is invalid and reported by setting name only.
/// </summary>
public sealed record ProposalSettings(ProposalLimits Limits)
{
    public const string MaxChangesVariable = "AGENTICCHANGEOPS_PROPOSAL_MAX_CHANGES";
    public const string MaxBytesVariable = "AGENTICCHANGEOPS_PROPOSAL_MAX_BYTES";

    /// <summary>
    /// Throws <see cref="GeneratorConfigurationException"/> naming the first setting that is wrong;
    /// the supplied value is never put in the message.
    /// </summary>
    public static ProposalSettings FromConfiguration(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        return new ProposalSettings(new ProposalLimits(
            ReadPositive(getVariable, MaxChangesVariable, ProposalLimits.DefaultMaxChanges),
            ReadPositive(getVariable, MaxBytesVariable, ProposalLimits.DefaultMaxTotalBytes)));
    }

    private static int ReadPositive(Func<string, string?> getVariable, string name, int defaultValue)
    {
        var raw = getVariable(name);
        if (raw is null)
            return defaultValue;

        if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
            || value <= 0)
            throw new GeneratorConfigurationException(name, "it must be a positive whole number");

        return value;
    }
}
