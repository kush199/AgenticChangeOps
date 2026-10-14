using System.ClientModel.Primitives;
using System.Net;
using System.Text;

namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>One request as it reached the (fake) network.</summary>
public sealed record RecordedRequest(
    string Method, string Path, IReadOnlyDictionary<string, string> Headers, string Body);

/// <summary>
/// A fake HTTP layer for the model clients: records every request that reaches it and answers
/// with the next scripted reply. Never touches a network. Holds only synthetic content.
/// </summary>
public sealed class RecordingTransport : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<RecordedRequest> _requests = new();
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _script = new();
    private Func<CancellationToken, Task<HttpResponseMessage>>? _fallback;
    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    public IReadOnlyList<RecordedRequest> Requests
    {
        get { lock (_gate) return _requests.ToArray(); }
    }

    /// <summary>The pipeline transport to hand to the client options.</summary>
    public PipelineTransport AsPipelineTransport() => new HttpClientPipelineTransport(new HttpClient(this));

    public RecordingTransport ReturnsCompletion(string text, string finishReason = "stop")
    {
        var body =
            "{\"id\":\"chatcmpl-synthetic\",\"object\":\"chat.completion\",\"created\":1700000000,\"model\":\"synthetic-model\"," +
            "\"choices\":[{\"index\":0,\"finish_reason\":\"" + finishReason + "\"," +
            "\"message\":{\"role\":\"assistant\",\"content\":" + System.Text.Json.JsonSerializer.Serialize(text) + "}}]," +
            "\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";
        return Then(_ => Task.FromResult(Json(HttpStatusCode.OK, body)));
    }

    public RecordingTransport ReturnsStatus(int statusCode, string body = "{}") =>
        Then(_ => Task.FromResult(Json((HttpStatusCode)statusCode, body)));

    public RecordingTransport Throws(Exception exception) => Then(_ => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Waits until the request is cancelled, then throws, as a hung service would.</summary>
    public RecordingTransport HangsUntilCancelled() => Then(async token =>
    {
        await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        return Json(HttpStatusCode.OK, "{}");
    });

    /// <summary>Applies to every request after the scripted ones are used up.</summary>
    public RecordingTransport Always(Func<CancellationToken, Task<HttpResponseMessage>> reply)
    {
        _fallback = reply;
        return this;
    }

    private RecordingTransport Then(Func<CancellationToken, Task<HttpResponseMessage>> reply)
    {
        lock (_gate) _script.Enqueue(reply);
        return this;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
            headers[header.Key] = string.Join(",", header.Value);

        Func<CancellationToken, Task<HttpResponseMessage>>? reply;
        lock (_gate)
        {
            _requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri?.AbsolutePath ?? string.Empty, headers, body));
            reply = _script.Count > 0 ? _script.Dequeue() : _fallback;
        }

        Interlocked.Increment(ref _callCount);

        if (reply is null)
            return Json(HttpStatusCode.InternalServerError, "{}");

        return await reply(cancellationToken).ConfigureAwait(false);
    }
}
