using System.Text.Json;

namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>
/// A stand-in tool session: three tools whose results are scripted, with every invocation counted.
/// </summary>
public sealed class FakeToolSession : IToolSession
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}").RootElement.Clone();

    private readonly object _gate = new();
    private readonly List<ToolCallRecord> _calls = new();
    private readonly List<string> _filesRead = new();
    private readonly List<string> _invoked = new();

    public FakeToolSession(
        int maxModelCalls = 6,
        TimeSpan? timeLimit = null,
        Func<string, string, string>? result = null,
        Func<string, string, Exception?>? failure = null)
    {
        MaxModelCalls = maxModelCalls;
        TimeLimit = timeLimit ?? TimeSpan.FromSeconds(60);
        var definitions = new[] { "list_files", "read_file", "search_text" }
            .Select(n => new ToolDefinition(n, "d", Schema)).ToArray();
        Tools = new ToolSet(definitions, (name, arguments, _) =>
        {
            lock (_gate)
                _invoked.Add(name);
            var error = failure?.Invoke(name, arguments);
            if (error is not null)
                throw error;
            return Task.FromResult(result?.Invoke(name, arguments) ?? "result of " + name);
        });
    }

    public ToolSet Tools { get; }

    public IReadOnlyList<ToolCallRecord> Calls
    {
        get { lock (_gate) return _calls.ToArray(); }
    }

    public IReadOnlyList<string> FilesRead
    {
        get { lock (_gate) return _filesRead.ToArray(); }
    }

    public int MaxModelCalls { get; }

    public TimeSpan TimeLimit { get; }

    public IReadOnlyList<string> Invoked
    {
        get { lock (_gate) return _invoked.ToArray(); }
    }

    public int Disposals { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}
