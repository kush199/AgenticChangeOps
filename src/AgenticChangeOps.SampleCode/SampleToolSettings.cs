using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode;

/// <summary>
/// Operator settings for the sample codebase tools. Off unless explicitly switched on. When off,
/// no other setting is read, so a bad tools setting cannot affect a server that has tools off.
/// </summary>
public sealed record SampleToolSettings(
    bool Enabled,
    int MaxToolCalls,
    int MaxModelCalls,
    int MaxReturnedBytes,
    int MaxFileBytes,
    TimeSpan TimeLimit,
    string? SampleFolder)
{
    public const string EnabledVariable = "AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED";
    public const string MaxCallsVariable = "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS";
    public const string MaxModelCallsVariable = "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_MODEL_CALLS";
    public const string MaxReturnedBytesVariable = "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_RETURNED_BYTES";
    public const string MaxFileBytesVariable = "AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_FILE_BYTES";
    public const string TimeLimitVariable = "AGENTICCHANGEOPS_SAMPLE_TOOLS_TIME_LIMIT_SECONDS";
    public const string SampleFolderVariable = "AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH";

    public const int DefaultMaxToolCalls = 8;
    public const int DefaultMaxModelCalls = 6;
    public const int DefaultMaxReturnedBytes = 24000;
    public const int DefaultMaxFileBytes = 8000;
    public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Reads and validates the settings. Throws
    /// <see cref="AgenticChangeOps.ModelAccess.GeneratorConfigurationException"/> naming the first
    /// setting that is wrong; the supplied value is never put in the message.
    /// </summary>
    public static SampleToolSettings FromConfiguration(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        var switchText = getVariable(EnabledVariable);
        if (switchText is null)
            return Off();

        var enabled = switchText.Trim().ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => throw new GeneratorConfigurationException(EnabledVariable, "it must be true or false")
        };

        // Nothing else is read while the switch is off, so a bad tools setting cannot affect a
        // server that has the tools off.
        if (!enabled)
            return Off();

        var folder = getVariable(SampleFolderVariable);
        return new SampleToolSettings(
            Enabled: true,
            MaxToolCalls: ReadPositive(getVariable, MaxCallsVariable, DefaultMaxToolCalls),
            MaxModelCalls: ReadPositive(getVariable, MaxModelCallsVariable, DefaultMaxModelCalls),
            MaxReturnedBytes: ReadPositive(getVariable, MaxReturnedBytesVariable, DefaultMaxReturnedBytes),
            MaxFileBytes: ReadPositive(getVariable, MaxFileBytesVariable, DefaultMaxFileBytes),
            TimeLimit: TimeSpan.FromSeconds(ReadPositive(getVariable, TimeLimitVariable, (int)DefaultTimeLimit.TotalSeconds)),
            SampleFolder: string.IsNullOrWhiteSpace(folder) ? null : folder.Trim());
    }

    private static SampleToolSettings Off() => new(
        Enabled: false,
        MaxToolCalls: DefaultMaxToolCalls,
        MaxModelCalls: DefaultMaxModelCalls,
        MaxReturnedBytes: DefaultMaxReturnedBytes,
        MaxFileBytes: DefaultMaxFileBytes,
        TimeLimit: DefaultTimeLimit,
        SampleFolder: null);

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
