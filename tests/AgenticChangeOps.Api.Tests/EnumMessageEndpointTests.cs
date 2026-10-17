using System.Net;
using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Over HTTP, an unrecognised priority, change type or decision gets one fixed message that names
/// the field and lists the accepted values, and nothing the caller sent comes back or is logged.
/// </summary>
public class EnumMessageEndpointTests : IDisposable
{
    private const string PriorityMessage =
        "Priority is not a recognised value. Accepted values: Low, Medium, High, Critical.";
    private const string ChangeTypeMessage =
        "ChangeType is not a recognised value. Accepted values: Feature, Bugfix, ConfigChange.";
    private const string DecisionMessage =
        "Decision is not a recognised value. Accepted values: Approve, Reject, Defer.";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public EnumMessageEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public static IEnumerable<object[]> UnrecognisedTexts() => new[]
    {
        new object[] { "urgent" },
        new object[] { "other" },
        new object[] { "7" },
        new object[] { "-1" },
        new object[] { "HOSTILE-MARKER-0001<script>alert(1)</script>" },
        new object[] { "LONG-MARKER-0002" + new string('Q', 5000) },
    };

    private static string MessageFor(JsonElement json, string field) =>
        json.GetProperty("errors").EnumerateArray()
            .Single(e => e.GetProperty("field").GetString() == field)
            .GetProperty("message").GetString()!;

    private async Task AssertNothingEchoedAsync(HttpResponseMessage response, string supplied)
    {
        var probe = supplied[..Math.Min(16, supplied.Length)];
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(supplied, body);
        if (probe.Length > 3)
            Assert.DoesNotContain(probe, body);
        Assert.DoesNotContain("-1", body);
        Assert.DoesNotContain("\\u0027", body);
        Assert.DoesNotContain("'", body);
        if (probe.Length > 3)
            Assert.All(_factory.Logs.Lines, line => Assert.DoesNotContain(probe, line));
    }

    [Theory]
    [MemberData(nameof(UnrecognisedTexts))]
    public async Task Priority_Unrecognised_GetsTheFixedMessageAndNothingIsEchoed(string supplied)
    {
        var response = await _client.PostJsonAsync(
            "/change-requests", SyntheticData.ValidSubmission() with { Priority = supplied });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        Assert.Equal(new[] { "Priority" }, json.FieldNames());
        Assert.Equal(PriorityMessage, MessageFor(json, "Priority"));
        await AssertNothingEchoedAsync(response, supplied);
    }

    [Theory]
    [MemberData(nameof(UnrecognisedTexts))]
    public async Task ChangeType_Unrecognised_GetsTheFixedMessageAndNothingIsEchoed(string supplied)
    {
        var response = await _client.PostJsonAsync(
            "/change-requests", SyntheticData.ValidSubmission() with { ChangeType = supplied });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(new[] { "ChangeType" }, json.FieldNames());
        Assert.Equal(ChangeTypeMessage, MessageFor(json, "ChangeType"));
        await AssertNothingEchoedAsync(response, supplied);
    }

    [Theory]
    [MemberData(nameof(UnrecognisedTexts))]
    public async Task Decision_Unrecognised_GetsTheFixedMessage_NothingIsEchoed_AndNoDecisionIsRecorded(string supplied)
    {
        var id = await _client.SubmitAsync();

        var response = await _client.PostJsonAsync(
            $"/change-requests/{id}/decisions", SyntheticData.Approve() with { Decision = supplied });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        Assert.Equal(new[] { "Decision" }, json.FieldNames());
        Assert.Equal(DecisionMessage, MessageFor(json, "Decision"));
        await AssertNothingEchoedAsync(response, supplied);

        var audit = await (await _client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        Assert.Equal(
            new[] { "Submitted" },
            audit.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray());
        var request = await (await _client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Submitted", request.GetProperty("status").GetString());
    }

    [Fact]
    public async Task NullOrOmittedValues_GetTheSameMessage_WithTheSameStatusCodeAndField()
    {
        var withNull = await _client.PostJsonAsync(
            "/change-requests", SyntheticData.ValidSubmission() with { Priority = null });
        var omitted = await _client.PostRawAsync(
            "/change-requests",
            "{\"title\":\"t\",\"description\":\"d\",\"requester\":\"r\",\"affectedSystem\":\"s\",\"changeType\":\"Feature\"}");

        foreach (var response in new[] { withNull, omitted })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.ReadJsonAsync();
            Assert.Equal("validation-failed", json.GetProperty("code").GetString());
            Assert.Equal(PriorityMessage, MessageFor(json, "Priority"));
        }
    }

    [Theory]
    [InlineData(" high ")]
    [InlineData("HIGH")]
    [InlineData("high")]
    public async Task CaseAndSpacingVariantsOfAcceptedValues_AreStillAccepted(string priority)
    {
        var submitted = await _client.PostJsonAsync(
            "/change-requests", SyntheticData.ValidSubmission() with { Priority = priority });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var id = (await submitted.ReadJsonAsync()).GetProperty("changeRequestId").GetGuid();

        var decided = await _client.PostJsonAsync(
            $"/change-requests/{id}/decisions", SyntheticData.Approve() with { Decision = " approve " });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
    }
}
