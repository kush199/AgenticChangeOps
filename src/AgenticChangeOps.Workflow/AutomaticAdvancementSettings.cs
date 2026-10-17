using System.Globalization;

namespace AgenticChangeOps.Workflow;

/// <summary>Operator settings for automatic advancement. Off unless explicitly switched on.</summary>
public sealed record AutomaticAdvancementSettings(
    bool Enabled,
    TimeSpan Interval,
    int MaxStepsPerCheck,
    TimeSpan FailureWait,
    int MaxConsecutiveFailures)
{
    public static AutomaticAdvancementSettings Defaults { get; } = new(
        Enabled: false,
        Interval: TimeSpan.FromMinutes(5),
        MaxStepsPerCheck: 5,
        FailureWait: TimeSpan.FromMinutes(15),
        MaxConsecutiveFailures: 3);

    public const string EnabledKey = "ChangeOps:AutoAdvance:Enabled";
    public const string IntervalKey = "ChangeOps:AutoAdvance:IntervalSeconds";
    public const string MaxStepsKey = "ChangeOps:AutoAdvance:MaxStepsPerCheck";
    public const string FailureWaitKey = "ChangeOps:AutoAdvance:FailureWaitSeconds";
    public const string MaxFailuresKey = "ChangeOps:AutoAdvance:MaxConsecutiveFailures";

    private const int SecondsPerDay = 86400;

    /// <summary>
    /// Reads the settings. An invalid value replaces that setting alone with its default and calls
    /// <paramref name="warn"/> with the setting key (never the value).
    /// </summary>
    public static AutomaticAdvancementSettings FromConfiguration(
        Func<string, string?> getValue, Action<string> warn)
    {
        var defaults = Defaults;

        var enabled = false;
        var rawEnabled = getValue(EnabledKey);
        if (!string.IsNullOrWhiteSpace(rawEnabled))
        {
            // Only an explicit boolean true switches the feature on; anything else is off.
            if (bool.TryParse(rawEnabled.Trim(), out var parsed))
                enabled = parsed;
            else
                warn(Warning(EnabledKey));
        }

        var interval = ReadSeconds(getValue, warn, IntervalKey, 1, SecondsPerDay, defaults.Interval);
        var maxSteps = ReadInt(getValue, warn, MaxStepsKey, 1, 1000, defaults.MaxStepsPerCheck);
        var failureWait = ReadSeconds(getValue, warn, FailureWaitKey, 0, SecondsPerDay, defaults.FailureWait);
        var maxFailures = ReadInt(getValue, warn, MaxFailuresKey, 1, 100, defaults.MaxConsecutiveFailures);

        return new AutomaticAdvancementSettings(enabled, interval, maxSteps, failureWait, maxFailures);
    }

    private static string Warning(string key) => $"Setting {key} is invalid; the default is used.";

    private static int ReadInt(
        Func<string, string?> getValue, Action<string> warn, string key, int min, int max, int fallback)
    {
        var raw = getValue(key);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max)
            return value;

        warn(Warning(key));
        return fallback;
    }

    private static TimeSpan ReadSeconds(
        Func<string, string?> getValue, Action<string> warn, string key, int min, int max, TimeSpan fallback)
    {
        var raw = getValue(key);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max)
            return TimeSpan.FromSeconds(value);

        warn(Warning(key));
        return fallback;
    }
}
