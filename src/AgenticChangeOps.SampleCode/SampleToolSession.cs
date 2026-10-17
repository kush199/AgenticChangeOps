using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using AgenticChangeOps.ModelAccess;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgenticChangeOps.SampleCode;

/// <summary>
/// One request's tools: a budget, a call recorder and an in-process MCP server and client joined
/// by in-memory pipes (no process, no network). The tool set is fixed when the session opens.
/// Tool results are only ever returned to the caller as text; nothing here reads them back into
/// instructions or the tool list.
/// </summary>
public sealed class SampleToolSession : IToolSession
{
    private const int MaxRecordedTargetLength = 120;
    private const string RefusedTarget = "(refused)";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly List<ToolCallRecord> _calls = new();
    private readonly List<string> _filesRead = new();
    private readonly SampleReader _reader;
    private readonly ToolBudget _budget;
    private readonly SampleToolSettings _settings;
    private McpServer? _server;
    private McpClient? _client;
    private Task? _serverRun;
    private CancellationTokenSource? _stop;
    private bool _disposed;

    private SampleToolSession(SampleToolSettings settings, string sampleRoot, TimeProvider? time)
    {
        _settings = settings;
        _reader = new SampleReader(new SamplePathResolver(sampleRoot), settings.MaxFileBytes);
        _budget = new ToolBudget(settings.MaxToolCalls, settings.MaxReturnedBytes, settings.TimeLimit, time);
        Tools = null!;
    }

    public ToolSet Tools { get; private set; }

    public IReadOnlyList<ToolCallRecord> Calls
    {
        get { lock (_gate) return _calls.ToArray(); }
    }

    public IReadOnlyList<string> FilesRead
    {
        get { lock (_gate) return _filesRead.ToArray(); }
    }

    public int MaxModelCalls => _settings.MaxModelCalls;

    public TimeSpan TimeLimit => _settings.TimeLimit;

    internal string TransportKind => "in-memory";

    internal static async Task<SampleToolSession> OpenAsync(
        SampleToolSettings settings, string sampleRoot, TimeProvider? time, CancellationToken cancellationToken)
    {
        var session = new SampleToolSession(settings, sampleRoot, time);
        try
        {
            await session.StartAsync(cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        _stop = new CancellationTokenSource();
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        startup.CancelAfter(StartupTimeout);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var options = SampleMcpServer.CreateOptions(
            path => Execute(SampleMcpServer.ListFiles, path, isPath: true, _reader.List),
            path => Execute(SampleMcpServer.ReadFile, path, isPath: true, _reader.Read),
            term => Execute(SampleMcpServer.SearchText, term, isPath: false, _reader.Search));

        _server = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()), options);
        _serverRun = _server.RunAsync(_stop.Token);

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            cancellationToken: startup.Token).ConfigureAwait(false);

        var listed = await _client.ListToolsAsync(cancellationToken: startup.Token).ConfigureAwait(false);
        if (!SampleMcpServer.IsExpectedToolSet(listed.Select(t => t.Name)))
            throw new InvalidOperationException("The tool server did not offer exactly the expected tools.");

        var definitions = listed
            .Select(t => new ToolDefinition(t.Name, t.Description ?? string.Empty, t.JsonSchema))
            .ToArray();
        Tools = new ToolSet(definitions, (name, argumentsJson, ct) => CallThroughClientAsync(name, argumentsJson, ct));
    }

    // One call: counted, run (or refused), sized, timed and recorded. Refusals come back as the
    // fixed text, never as an exception.
    private string Execute(string tool, string? argument, bool isPath, Func<string?, ToolResult> run)
    {
        var startedAt = DateTime.UtcNow;
        var clock = Stopwatch.StartNew();

        ToolResult result;
        var refusal = _budget.BeginCall();
        if (refusal is { } blocked)
        {
            result = new ToolResult(blocked, ToolMessages.For(blocked), 0);
        }
        else
        {
            result = run(argument);
            if (result.Outcome == ToolCallOutcome.Served && !_budget.TryTakeBytes(result.Bytes))
            {
                var outcome = _budget.TimeIsUp() ? ToolCallOutcome.RefusedTimeLimit : ToolCallOutcome.RefusedDataLimit;
                result = new ToolResult(outcome, ToolMessages.For(outcome), 0);
            }
        }

        var record = new ToolCallRecord(
            tool, TargetFor(argument, isPath), result.Outcome, result.Bytes, clock.Elapsed, startedAt);
        lock (_gate)
        {
            _calls.Add(record);
            if (result.Outcome == ToolCallOutcome.Served && result.FileRead is { } file && !_filesRead.Contains(file))
                _filesRead.Add(file);
        }

        return result.Text;
    }

    // What is recorded for a call: a path only when it is a valid path inside the sample, otherwise
    // a fixed placeholder, so nothing the model typed outside the sample is ever kept.
    private string TargetFor(string? argument, bool isPath)
    {
        if (isPath)
        {
            if (argument is null)
                return ".";

            var resolution = _reader.Resolver.Resolve(argument);
            return resolution.IsAllowed ? resolution.RelativePath! : RefusedTarget;
        }

        if (string.IsNullOrWhiteSpace(argument))
            return RefusedTarget;

        var cleaned = new string(argument.Where(c => !char.IsControl(c)).ToArray());
        return cleaned.Length > MaxRecordedTargetLength ? cleaned[..MaxRecordedTargetLength] : cleaned;
    }

    private async Task<string> CallThroughClientAsync(string name, string argumentsJson, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var arguments = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                    arguments[property.Name] = property.Value.Clone();
            }
        }

        var result = await _client!.CallToolAsync(name, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (block is TextContentBlock textBlock)
                text.Append(textBlock.Text);
        }

        return text.ToString();
    }

    /// <summary>Calls a tool through the MCP client, as the model would. For tests.</summary>
    internal Task<string> CallAsync(
        string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return CallThroughClientAsync(toolName, JsonSerializer.Serialize(arguments), cancellationToken);
    }

    /// <summary>What the in-process server offers: tool names, resource count and prompt count. For tests.</summary>
    internal async Task<(IReadOnlyList<string> Tools, int Resources, int Prompts)> DescribeServerAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var client = _client!;
        var tools = (await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            .Select(t => t.Name).ToArray();
        var resources = client.ServerCapabilities.Resources is null
            ? 0
            : (await client.ListResourcesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Count;
        var prompts = client.ServerCapabilities.Prompts is null
            ? 0
            : (await client.ListPromptsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Count;
        return (tools, resources, prompts);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _stop?.Cancel();
            if (_client is not null)
                await _client.DisposeAsync().ConfigureAwait(false);
            if (_server is not null)
                await _server.DisposeAsync().ConfigureAwait(false);
            if (_serverRun is not null)
                await Task.WhenAny(_serverRun, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or IOException)
        {
            // Closing a pipe that is already closed is the normal way a session ends.
        }
        finally
        {
            _stop?.Dispose();
        }
    }
}
