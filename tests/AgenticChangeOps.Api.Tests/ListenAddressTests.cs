using System.Net;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class ListenAddressTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ListenAddressTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithNoOperatorSetting_IsLoopbackOnly(string? configured)
    {
        var url = ListenAddress.Resolve(name => name == ListenAddress.UrlsVariable ? configured : null);

        Assert.Equal("http://127.0.0.1:5080", url);
        Assert.Equal(ListenAddress.DefaultUrl, url);
    }

    [Fact]
    public void Resolve_WithOperatorSetting_ReturnsItUnchanged()
    {
        var url = ListenAddress.Resolve(name =>
            name == ListenAddress.UrlsVariable ? "http://0.0.0.0:6000" : null);

        Assert.Equal("http://0.0.0.0:6000", url);
    }

    [Fact]
    public async Task CrossOriginRequest_ReceivesNoAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/change-requests/not-a-guid");
        request.Headers.Add("Origin", "http://other.example");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_IsNotGrantedCrossOriginAccess()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/change-requests");
        request.Headers.Add("Origin", "http://other.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Methods"));
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("192.0.2.10")]
    public async Task ForeignHostHeader_IsRefusedWithAJson400(string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/change-requests/not-a-guid");
        request.Headers.Host = host;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-host", json.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1:5080")]
    public async Task LocalHostHeader_IsAccepted(string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/change-requests/not-a-guid");
        request.Headers.Host = host;

        var response = await _client.SendAsync(request);

        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-identifier", json.GetProperty("code").GetString());
    }
}
