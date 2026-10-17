namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// The tools for one request and what was done with them. Opened per request and disposed after
/// it, so nothing is carried from one request to the next.
/// </summary>
public interface IToolSession : IAsyncDisposable
{
    /// <summary>
    /// The model-facing tools, fixed when the session was opened. Tool results can never add to or
    /// change this set (a tool result is only ever a tool-result message).
    /// </summary>
    ToolSet Tools { get; }

    /// <summary>Every call made so far, in call order.</summary>
    IReadOnlyList<ToolCallRecord> Calls { get; }

    /// <summary>Relative paths of the files successfully read, without duplicates, in first-read order.</summary>
    IReadOnlyList<string> FilesRead { get; }

    /// <summary>The most model calls the request may make.</summary>
    int MaxModelCalls { get; }

    /// <summary>How long the whole request may take.</summary>
    TimeSpan TimeLimit { get; }
}
