namespace AgenticChangeOps.ModelAccess;

public enum GenerationFailureCategory
{
    Unreachable,
    TimedOut,
    ResponseTooLarge,
    MalformedOutput,
    ToolsUnavailable,
    LimitsExhausted
}
