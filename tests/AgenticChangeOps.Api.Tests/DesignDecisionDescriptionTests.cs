using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>The published description lists the new operation and the optional decision on designs.</summary>
public sealed class DesignDecisionDescriptionTests : IDisposable
{
    private const string Route = "/change-requests/{id}/designs/{version}/decisions";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public DesignDecisionDescriptionTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<JsonElement> DescriptionAsync() =>
        await (await _client.GetAsync("/openapi/v1.json")).ReadJsonAsync();

    [Fact]
    public async Task TheNewOperation_IsListed_WithItsBodyAndResponses()
    {
        var post = (await DescriptionAsync()).GetProperty("paths").GetProperty(Route).GetProperty("post");

        Assert.Equal("RecordDesignDecision", post.GetProperty("operationId").GetString());
        Assert.Equal("Record an Accept or Reject decision on a design draft", post.GetProperty("summary").GetString());
        Assert.True(post.TryGetProperty("requestBody", out _));
        var responses = post.GetProperty("responses").EnumerateObject().Select(r => r.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "201", "400", "404", "409" }, responses);
    }

    [Fact]
    public async Task TheTwoDesignReads_ReferenceTheViewWithAnOptionalDecision()
    {
        var json = await DescriptionAsync();
        var text = json.GetRawText();

        Assert.Contains("\"#/components/schemas/DesignView\"", text);
        var view = json.GetProperty("components").GetProperty("schemas").GetProperty("DesignView");
        var properties = view.GetProperty("properties");
        Assert.True(properties.TryGetProperty("decision", out _));
        var required = view.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(x => x.GetString()).ToArray()
            : Array.Empty<string?>();
        Assert.DoesNotContain("decision", required);
        foreach (var path in new[] { "/change-requests/{id}/designs", "/change-requests/{id}/designs/latest" })
        {
            var schema = json.GetProperty("paths").GetProperty(path).GetProperty("get")
                .GetProperty("responses").GetProperty("200").GetRawText();
            Assert.Contains("DesignView", schema);
        }
    }

    [Fact]
    public async Task TheAuditEventTypeEnum_GainsDesignDecidedAtTheEnd()
    {
        var values = (await DescriptionAsync()).GetProperty("components").GetProperty("schemas")
            .GetProperty("AuditEventType").GetProperty("enum").EnumerateArray().Select(v => v.GetInt32()).ToArray();

        Assert.Equal(Enumerable.Range(0, 10), values);
    }
}
