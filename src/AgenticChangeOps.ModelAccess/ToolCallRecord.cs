namespace AgenticChangeOps.ModelAccess;

public enum ToolCallOutcome
{
    Served,
    RefusedOutsideSample,
    RefusedNotFound,
    RefusedTooLarge,
    RefusedNotText,
    RefusedCallLimit,
    RefusedDataLimit,
    RefusedTimeLimit,
    RefusedInvalidArgument
}

/// <summary>
/// One tool call made while preparing content: which tool, what it was pointed at, how it ended,
/// how many bytes came back and how long it took. There is deliberately no member that could hold
/// what the tool returned.
/// </summary>
public sealed record ToolCallRecord(
    string ToolName,
    string Target,
    ToolCallOutcome Outcome,
    int ResultBytes,
    TimeSpan Elapsed,
    DateTime StartedAt);
