using System.Globalization;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// Run-time settings for the model-backed generator. Endpoint and deployment name are required
/// and never stored in the repository; the limits have documented defaults.
/// </summary>
public sealed record AzureOpenAIGeneratorOptions(
    Uri Endpoint,
    string DeploymentName,
    TimeSpan RequestTimeout,
    int MaxResponseCharacters)
{
    public const string EndpointVariable = "AGENTICCHANGEOPS_MODEL_ENDPOINT";
    public const string DeploymentVariable = "AGENTICCHANGEOPS_MODEL_DEPLOYMENT";
    public const string TimeoutVariable = "AGENTICCHANGEOPS_MODEL_TIMEOUT_SECONDS";
    public const string MaxResponseCharactersVariable = "AGENTICCHANGEOPS_MODEL_MAX_RESPONSE_CHARS";

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    public const int DefaultMaxResponseCharacters = 32000;

    /// <summary>
    /// Reads and validates every setting. Throws <see cref="GeneratorConfigurationException"/>
    /// naming the first setting that is wrong; the supplied value is never put in the message.
    /// </summary>
    public static AzureOpenAIGeneratorOptions FromEnvironment(Func<string, string?> getVariable)
    {
        var endpointText = ReadRequired(getVariable, EndpointVariable);
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new GeneratorConfigurationException(EndpointVariable, "must be an absolute https address");

        var deployment = ReadRequired(getVariable, DeploymentVariable);

        var timeoutSeconds = ReadPositiveInteger(getVariable, TimeoutVariable, defaultValue: null);
        var maxCharacters = ReadPositiveInteger(getVariable, MaxResponseCharactersVariable, DefaultMaxResponseCharacters);

        return new AzureOpenAIGeneratorOptions(
            endpoint,
            deployment,
            timeoutSeconds is null ? DefaultRequestTimeout : TimeSpan.FromSeconds(timeoutSeconds.Value),
            maxCharacters!.Value);
    }

    private static string ReadRequired(Func<string, string?> getVariable, string name)
    {
        var value = getVariable(name)?.Trim();
        if (string.IsNullOrEmpty(value))
            throw new GeneratorConfigurationException(name, "it is missing or blank");

        if (IsPlaceholder(value))
            throw new GeneratorConfigurationException(name, "it still holds a placeholder value");

        return value;
    }

    private static int? ReadPositiveInteger(Func<string, string?> getVariable, string name, int? defaultValue)
    {
        var raw = getVariable(name);
        if (raw is null)
            return defaultValue;

        if (!int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new GeneratorConfigurationException(name, "it must be a positive whole number");

        return value;
    }

    // Placeholder forms used by documentation and sample configuration: angle-bracket or brace
    // wrapped, a YOUR_ or REPLACE prefix, or the word "placeholder" anywhere.
    private static bool IsPlaceholder(string value) =>
        (value.StartsWith('<') && value.EndsWith('>')) ||
        (value.StartsWith('{') && value.EndsWith('}')) ||
        value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
}
