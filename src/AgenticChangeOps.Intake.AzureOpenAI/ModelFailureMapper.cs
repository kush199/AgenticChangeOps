using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.AzureOpenAI;

/// <summary>
/// Maps service and client errors onto the four failure categories. The error code is only
/// compared, never retained, so nothing from the service response travels onward.
/// </summary>
public static class ModelFailureMapper
{
    private const string ContentFilterCode = "content_filter";
    private const int RequestTimeoutStatus = 408;

    public static GenerationFailureCategory FromError(int statusCode, string? errorCode)
    {
        if (string.Equals(errorCode, ContentFilterCode, StringComparison.OrdinalIgnoreCase))
            return GenerationFailureCategory.MalformedOutput;

        if (statusCode == RequestTimeoutStatus)
            return GenerationFailureCategory.TimedOut;

        return GenerationFailureCategory.Unreachable;
    }

    public static GenerationFailureCategory FromException(Exception exception) =>
        exception is TimeoutException
            ? GenerationFailureCategory.TimedOut
            : GenerationFailureCategory.Unreachable;
}
