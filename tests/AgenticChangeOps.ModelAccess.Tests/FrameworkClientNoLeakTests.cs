using System.Net.Http;
using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// FR-008: no model text, endpoint, deployment name or credential in any error, and nothing written
/// to the console or trace output (the client has no logger, so output is the only other channel).
/// </summary>
[Collection("ConsoleCapture")]
public sealed class FrameworkClientNoLeakTests
{
    private const string AnswerMarker = "sentinel-answer-text-5e0a";
    private const string DetailMarker = "sentinel-detail-3b9f";

    private sealed class TraceCapture : System.Diagnostics.TraceListener
    {
        public System.Text.StringBuilder Text { get; } = new();
        public override void Write(string? message) => Text.Append(message);
        public override void WriteLine(string? message) => Text.AppendLine(message);
    }

    private static void AssertClean(string text)
    {
        foreach (var forbidden in new[] { AnswerMarker, DetailMarker, "sentinel-endpoint", "sentinel-deployment", "synthetic-token" })
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }

    private static void Walk(Exception? exception, List<string> into)
    {
        while (exception is not null)
        {
            into.Add(exception.Message);
            into.Add(exception.ToString());
            foreach (var value in exception.Data.Values) into.Add(value?.ToString() ?? string.Empty);
            exception = exception.InnerException;
        }
    }

    [Fact]
    public async Task FailingCall_ExposesNothingThroughTheExceptionOrAnyOutput()
    {
        var transport = new RecordingTransport()
            .ReturnsStatus(500, "{\"error\":{\"message\":\"" + DetailMarker + " " + AnswerMarker + "\"}}");
        var client = ClientHarness.Framework(transport);

        var oldOut = Console.Out;
        var oldErr = Console.Error;
        var captured = new StringWriter();
        var trace = new TraceCapture();
        DraftGenerationException ex;
        try
        {
            Console.SetOut(captured);
            Console.SetError(captured);
            System.Diagnostics.Trace.Listeners.Add(trace);

            ex = await Assert.ThrowsAsync<DraftGenerationException>(() => client.CompleteAsync(
                ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None));
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(trace);
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }

        var seen = new List<string>();
        Walk(ex, seen);
        seen.Add(captured.ToString());
        seen.Add(trace.Text.ToString());
        foreach (var text in seen) AssertClean(text);
        Assert.Equal(string.Empty, captured.ToString());
    }

    [Fact]
    public async Task ThrownTransportException_ExposesNothing()
    {
        var transport = new RecordingTransport().Throws(
            new HttpRequestException(DetailMarker + " https://sentinel-endpoint.invalid/ sentinel-deployment synthetic-token"));
        var client = ClientHarness.Framework(transport);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => client.CompleteAsync(
            ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None));

        var seen = new List<string>();
        Walk(ex, seen);
        foreach (var text in seen) AssertClean(text);
    }
}
