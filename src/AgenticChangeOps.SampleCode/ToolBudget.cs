using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode;

/// <summary>
/// Per-request ceilings on tool calls, bytes returned and elapsed time. Thread-safe. A refused
/// call still counts as a call.
/// </summary>
public sealed class ToolBudget
{
    private readonly object _gate = new();
    private readonly DateTimeOffset _started;
    private int _calls;
    private int _bytes;

    public ToolBudget(int maxCalls, int maxBytes, TimeSpan timeLimit, TimeProvider? time = null)
    {
        MaxCalls = maxCalls;
        MaxBytes = maxBytes;
        TimeLimit = timeLimit;
        Time = time ?? TimeProvider.System;
        _started = Time.GetUtcNow();
    }

    public int MaxCalls { get; }
    public int MaxBytes { get; }
    public TimeSpan TimeLimit { get; }
    public TimeProvider Time { get; }

    public int CallsUsed
    {
        get { lock (_gate) return _calls; }
    }

    public int BytesReturned
    {
        get { lock (_gate) return _bytes; }
    }

    /// <summary>Counts a call. Returns null when it may go ahead, otherwise the refusal.</summary>
    public ToolCallOutcome? BeginCall()
    {
        lock (_gate)
        {
            _calls++;
            if (TimeIsUp())
                return ToolCallOutcome.RefusedTimeLimit;
            if (_calls > MaxCalls)
                return ToolCallOutcome.RefusedCallLimit;
            return null;
        }
    }

    /// <summary>Takes the bytes from the data budget, or refuses the whole result.</summary>
    public bool TryTakeBytes(int bytes)
    {
        lock (_gate)
        {
            if (TimeIsUp() || bytes < 0 || (long)_bytes + bytes > MaxBytes)
                return false;

            _bytes += bytes;
            return true;
        }
    }

    /// <summary>True once the time limit has passed.</summary>
    public bool TimeIsUp()
    {
        return Time.GetUtcNow() - _started >= TimeLimit;
    }
}
