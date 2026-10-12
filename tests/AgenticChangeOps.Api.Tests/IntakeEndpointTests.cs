using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class IntakeEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public IntakeEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Submit_ValidBody_Returns201WithLocationAndStoredRequest()
    {
        var response = await _client.PostJsonAsync("/change-requests", SyntheticData.ValidSubmission());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        var id = json.GetProperty("changeRequestId").GetGuid();
        Assert.Equal($"/change-requests/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Submitted", json.GetProperty("status").GetString());
        Assert.Equal("Rotate demo cache settings", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Get_ExistingRequest_Returns200WithSameContentAsSubmit()
    {
        var submit = await _client.PostJsonAsync("/change-requests", SyntheticData.ValidSubmission());
        var created = await submit.ReadJsonAsync();
        var id = created.GetProperty("changeRequestId").GetGuid();

        var response = await _client.GetAsync($"/change-requests/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.ReadJsonAsync();
        Assert.Equal(created.GetRawText(), fetched.GetRawText());
    }

    [Fact]
    public async Task Audit_ReturnsEntriesInRecordedOrder_SubmittedFirst()
    {
        var id = await _client.SubmitAsync();
        await _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Defer());

        var response = await _client.GetAsync($"/change-requests/{id}/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = (await response.ReadJsonAsync()).EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal("Submitted", entries[0].GetProperty("eventType").GetString());
        Assert.Equal("Deferred", entries[1].GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Submit_EmptyTitleAndBadPriority_Returns400NamingBothFields()
    {
        var body = SyntheticData.ValidSubmission() with { Title = "", Priority = "Urgent" };

        var response = await _client.PostJsonAsync("/change-requests", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        var fields = json.FieldNames();
        Assert.Contains("Title", fields);
        Assert.Contains("Priority", fields);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("[]")]
    public async Task Submit_MalformedOrEmptyBody_Returns400InvalidBody(string raw)
    {
        var response = await _client.PostRawAsync("/change-requests", raw);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-body", json.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/change-requests/not-a-guid")]
    [InlineData("/change-requests/not-a-guid/audit")]
    public async Task Get_InvalidIdentifier_Returns400InvalidIdentifier(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-identifier", json.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/change-requests/00000000-0000-0000-0000-000000000001")]
    [InlineData("/change-requests/00000000-0000-0000-0000-000000000001/audit")]
    public async Task Get_UnknownIdentifier_Returns404NotFound(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("not-found", json.GetProperty("code").GetString());
    }
}
