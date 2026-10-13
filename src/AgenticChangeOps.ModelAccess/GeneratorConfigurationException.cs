namespace AgenticChangeOps.Intake.AzureOpenAI;

/// <summary>
/// Raised when generator configuration is missing or invalid. The message names the setting
/// and the problem, never the supplied value.
/// </summary>
public sealed class GeneratorConfigurationException : Exception
{
    public GeneratorConfigurationException(string settingName, string problem)
        : base($"Setting {settingName} is invalid: {problem}.")
    {
        SettingName = settingName;
    }

    public string SettingName { get; }
}
