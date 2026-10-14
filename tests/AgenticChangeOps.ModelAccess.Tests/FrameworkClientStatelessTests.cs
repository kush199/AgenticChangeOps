using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>FR-004: every request is independent; nothing from one request reaches another.</summary>
public sealed class FrameworkClientStatelessTests
{
    [Fact]
    public async Task SecondRequest_ContainsNothingFromTheFirst_AndEqualsTheSameCallOnAFreshClient()
    {
        var transport = new RecordingTransport().ReturnsCompletion("{\"first\":1}").ReturnsCompletion("{\"second\":2}");
        var client = ClientHarness.Framework(transport);

        await client.CompleteAsync("first-system-marker", "first-user-marker", 100, CancellationToken.None);
        await client.CompleteAsync("second-system-marker", "second-user-marker", 100, CancellationToken.None);

        var second = transport.Requests[1].Body;
        Assert.DoesNotContain("first-system-marker", second, StringComparison.Ordinal);
        Assert.DoesNotContain("first-user-marker", second, StringComparison.Ordinal);
        Assert.DoesNotContain("{\\\"first\\\":1}", second, StringComparison.Ordinal);

        var freshTransport = new RecordingTransport().ReturnsCompletion("{\"second\":2}");
        await ClientHarness.Framework(freshTransport)
            .CompleteAsync("second-system-marker", "second-user-marker", 100, CancellationToken.None);

        Assert.Equal(
            WireSnapshot.Describe(freshTransport.Requests[0]),
            WireSnapshot.Describe(transport.Requests[1]));
    }

    [Fact]
    public async Task ParallelRequests_EachContainOnlyTheirOwnText()
    {
        var transport = new RecordingTransport().Always(_ => Task.FromResult(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(
                    "{\"id\":\"c\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"m\"," +
                    "\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}]}")
            }));
        var client = ClientHarness.Framework(transport);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            client.CompleteAsync($"system-{i:D2}-end", $"user-{i:D2}-end", 100, CancellationToken.None)));

        Assert.Equal(20, transport.CallCount);
        foreach (var request in transport.Requests)
        {
            var own = System.Text.RegularExpressions.Regex.Matches(request.Body, @"system-(\d\d)-end")
                .Select(m => m.Groups[1].Value).Distinct().ToList();
            var user = System.Text.RegularExpressions.Regex.Matches(request.Body, @"user-(\d\d)-end")
                .Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.Single(own);
            Assert.Equal(own, user);
        }
    }
}
