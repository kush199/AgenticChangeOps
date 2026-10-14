using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// contracts/wire-parity.md: the framework client and the hand-written client are run with the same
/// inputs over the same kind of fake transport and what reaches the wire is compared.
/// </summary>
public sealed class FrameworkClientWireParityTests
{
    private static async Task<(RecordedRequest Request, FakeTokenCredential Credential)> RunAsync(bool framework)
    {
        var transport = new RecordingTransport().ReturnsCompletion("{\"ok\":true}");
        var credential = new FakeTokenCredential();
        IChatModelClient client = framework
            ? ClientHarness.Framework(transport, credential)
            : ClientHarness.HandWritten(transport, credential);

        await client.CompleteAsync(
            ClientHarness.SystemText, ClientHarness.UserText, ClientHarness.MaxTokens, CancellationToken.None);

        return (Assert.Single(transport.Requests), credential);
    }

    private static bool IsAllowedDifference(string line) =>
        line.StartsWith("header: ", StringComparison.Ordinal) ||
        line.StartsWith("body.max_tokens = ", StringComparison.Ordinal) ||
        line.StartsWith("body.max_completion_tokens = ", StringComparison.Ordinal);

    [Fact]
    public async Task OneRequest_SameOperation_SameMessagesAndStructure()
    {
        var (hand, _) = await RunAsync(framework: false);
        var (fw, _) = await RunAsync(framework: true);

        Assert.Equal(hand.Method, fw.Method);
        Assert.Equal(hand.Path, fw.Path);

        var handLines = WireSnapshot.BodyLines(hand.Body);
        var fwLines = WireSnapshot.BodyLines(fw.Body);

        // Messages: count, order, roles and text, verbatim.
        Assert.Equal(
            handLines.Where(l => l.StartsWith("body.messages", StringComparison.Ordinal)),
            fwLines.Where(l => l.StartsWith("body.messages", StringComparison.Ordinal)));
        Assert.Contains("body.response_format.type = \"json_object\"", fwLines);
        Assert.DoesNotContain(fwLines, l => l.StartsWith("body.tools", StringComparison.Ordinal) ||
                                           l.StartsWith("body.tool_choice", StringComparison.Ordinal) ||
                                           l.StartsWith("body.functions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OutputTokenCap_HasTheRequestedValue_UnderWhicheverNameIsUsed()
    {
        var (fw, _) = await RunAsync(framework: true);

        var lines = WireSnapshot.BodyLines(fw.Body);
        Assert.Contains(lines, l =>
            l == $"body.max_tokens = {ClientHarness.MaxTokens}" ||
            l == $"body.max_completion_tokens = {ClientHarness.MaxTokens}");
    }

    [Fact]
    public async Task EveryDifferenceBetweenTheTwoRequests_IsOnTheAllowedList_AndIsRecorded()
    {
        var (hand, _) = await RunAsync(framework: false);
        var (fw, _) = await RunAsync(framework: true);

        var handLines = WireSnapshot.Describe(hand).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fwLines = WireSnapshot.Describe(fw).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var onlyHand = handLines.Except(fwLines).Select(l => "hand-written only: " + l.TrimEnd('\r'));
        var onlyFramework = fwLines.Except(handLines).Select(l => "framework only: " + l.TrimEnd('\r'));

        var differences = onlyHand.Concat(onlyFramework).OrderBy(l => l, StringComparer.Ordinal).ToList();
        var unexpected = differences
            .Where(d => !IsAllowedDifference(d[(d.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        Assert.True(unexpected.Count == 0, "Unexpected wire differences: " + string.Join(" | ", unexpected));

        WireSnapshot.VerifyGolden("framework-wire-diff.txt", string.Join("\n", differences) + "\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeylessSignIn_BearerTokenOnce_NoApiKeyHeader(bool framework)
    {
        var (request, credential) = await RunAsync(framework);

        Assert.True(request.Headers.ContainsKey("Authorization"));
        Assert.StartsWith("Bearer ", request.Headers["Authorization"], StringComparison.Ordinal);
        Assert.False(request.Headers.ContainsKey("api-key"));
        Assert.Equal(1, credential.TokenRequests);
    }
}
