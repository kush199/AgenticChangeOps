using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode;

/// <summary>
/// The fixed text a refused tool call returns to the model. Never contains the supplied argument,
/// a real path or a setting value.
/// </summary>
public static class ToolMessages
{
    public const string OutsideSample = "That path is outside the sample codebase.";
    public const string NotFound = "No such file or folder in the sample codebase.";
    public const string TooLarge = "That file is larger than the per-file size limit.";
    public const string NotText = "That file is not readable text.";
    public const string CallLimit = "The tool-call limit for this request has been reached.";
    public const string DataLimit = "The returned-data limit for this request has been reached.";
    public const string TimeLimit = "The time limit for this request has been reached.";
    public const string InvalidArgument = "That argument is not valid.";

    public static string For(ToolCallOutcome outcome) => outcome switch
    {
        ToolCallOutcome.RefusedOutsideSample => OutsideSample,
        ToolCallOutcome.RefusedNotFound => NotFound,
        ToolCallOutcome.RefusedTooLarge => TooLarge,
        ToolCallOutcome.RefusedNotText => NotText,
        ToolCallOutcome.RefusedCallLimit => CallLimit,
        ToolCallOutcome.RefusedDataLimit => DataLimit,
        ToolCallOutcome.RefusedTimeLimit => TimeLimit,
        ToolCallOutcome.RefusedInvalidArgument => InvalidArgument,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
