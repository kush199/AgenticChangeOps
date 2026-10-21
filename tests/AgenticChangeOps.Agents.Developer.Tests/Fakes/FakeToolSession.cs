using System.Text.Json;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Fakes;

/// <summary>
/// A stand-in tool session. A fake tool client (or the test) records calls on it directly; it
/// never touches a file. Disposals are counted so tests can prove a session is always closed.
/// </summary>
public sealed class FakeToolSession : IToolSession
{
    private static readonly JsonElement Schema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone();

    private readonly object _gate = new();
    private readonly List<ToolCallRecord> _calls = new();
    private readonly List<string> _filesRead = new();

    public FakeToolSession(int maxModelCalls = 6, TimeSpan? timeLimit = null)
    {
        MaxModelCalls = maxModelCalls;
        TimeLimit = timeLimit ?? TimeSpan.FromSeconds(60);
        Tools = new ToolSet(
            new[] { "list_files", "read_file", "search_text" }.Select(n => new ToolDefinition(n, "d", Schema)).ToArray(),
            (_, _, _) => Task.FromResult("fake"));
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

    public int Disposals { get; private set; }

    public static readonly DateTime FixedStart = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Records a served read of a file (and lists it as read), as the real session would.</summary>
    public FakeToolSession Read(string path, int bytes = 120, int millis = 3)
    {
        lock (_gate)
        {
            _calls.Add(new ToolCallRecord("read_file", path, ToolCallOutcome.Served, bytes, TimeSpan.FromMilliseconds(millis), FixedStart));
            if (!_filesRead.Contains(path))
                _filesRead.Add(path);
        }

        return this;
    }

    public FakeToolSession List(string path, int bytes = 60) => Add("list_files", path, ToolCallOutcome.Served, bytes);

    public FakeToolSession Search(string term, int bytes = 80) => Add("search_text", term, ToolCallOutcome.Served, bytes);

    public FakeToolSession Refused(string tool, string target, ToolCallOutcome outcome) => Add(tool, target, outcome, 0);

    private FakeToolSession Add(string tool, string target, ToolCallOutcome outcome, int bytes)
    {
        lock (_gate)
            _calls.Add(new ToolCallRecord(tool, target, outcome, bytes, TimeSpan.FromMilliseconds(2), FixedStart));
        return this;
    }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Opens the same fake session for a request, or reports the tools unavailable.</summary>
public sealed class FakeToolSessionFactory : IToolSessionFactory
{
    public List<FakeToolSession> Opened { get; } = new();

    public Func<FakeToolSession> Create { get; set; } = () => new FakeToolSession();

    public Exception? FailWith { get; set; }

    public Task<IToolSession> OpenAsync(CancellationToken cancellationToken)
    {
        if (FailWith is not null)
            return Task.FromException<IToolSession>(FailWith);

        var session = Create();
        Opened.Add(session);
        return Task.FromResult<IToolSession>(session);
    }
}

/// <summary>Stand-in for the tool-capable model call. Behaviour is scripted per test.</summary>
public sealed class FakeToolChatModelClient : IToolChatModelClient
{
    private int _callCount;

    public int CallCount => _callCount;
    public string? SystemInstruction { get; private set; }
    public string? UserContent { get; private set; }

    public Func<FakeToolSession, CancellationToken, Task<ChatModelResponse>> Behavior { get; set; } =
        (_, _) => Task.FromResult(new ChatModelResponse(FakeProposalGenerator.ValidAnswerJson(), false));

    public Task<ChatModelResponse> CompleteAsync(
        string systemInstruction, string userContent, int maxOutputTokens, IToolSession session, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        SystemInstruction = systemInstruction;
        UserContent = userContent;
        return Behavior((FakeToolSession)session, cancellationToken);
    }
}

/// <summary>Stand-in plain model client; the proposal generator never uses it for tool requests.</summary>
public sealed class FakeChatModelClient : IChatModelClient
{
    private int _callCount;

    public int CallCount => _callCount;

    public Task<ChatModelResponse> CompleteAsync(
        string systemInstruction, string userContent, int maxOutputTokens, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(new ChatModelResponse(FakeProposalGenerator.ValidAnswerJson(), false));
    }
}
