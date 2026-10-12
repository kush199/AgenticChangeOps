using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class EnumParsingTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public EnumParsingTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact(Skip = "synthetic")]
    public async Task Priority_IsParsedCaseInsensitively()
    {
        var body = SyntheticData.ValidSubmission() with { Priority = "high", ChangeType = "bugfix" };

        var response = await _client.PostJsonAsync("/change-requests", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("High", json.GetProperty("priority").GetString());
        Assert.Equal("Bugfix", json.GetProperty("changeType").GetString());
    }

    [Theory(Skip = "synthetic")]
    [InlineData("Urgent")]
    [InlineData("99")]
    [InlineData("-1")]
    public async Task UnparseableOrOutOfRangeValue_IsReportedByFieldName(string value)
    {
        var body = SyntheticData.ValidSubmission() with { ChangeType = value };

        var response = await _client.PostJsonAsync("/change-requests", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        Assert.Equal(new[] { "ChangeType" }, json.FieldNames());
    }

    [Fact(Skip = "synthetic")]
    public async Task MissingProperties_AreReportedAsRequired()
    {
        var response = await _client.PostRawAsync("/change-requests", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        var fields = json.FieldNames();
        foreach (var expected in new[] { "Title", "Description", "Requester", "AffectedSystem" })
            Assert.Contains(expected, fields);
    }
}
