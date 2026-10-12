using System.Net;
using System.Text;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

public class ErrorShapeTests : IDisposable
{
    private readonly ApiFactory _factory = new(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
    private readonly HttpClient _client;

    public ErrorShapeTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact(Skip = "synthetic")]
    public async Task UnknownRoute_Returns404RouteNotFoundAsJson()
    {
        var response = await _client.GetAsync("/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("route-not-found", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task UnsupportedMethod_Returns405MethodNotAllowedAsJson()
    {
        var response = await _client.PutAsync("/change-requests", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("method-not-allowed", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task OversizedBody_Returns413BodyTooLargeAsJson()
    {
        var padding = new string('x', 70 * 1024);
        var raw = $"{{\"title\":\"{padding}\"}}";

        var response = await _client.PostRawAsync("/change-requests", raw);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("body-too-large", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task NonJsonBody_Returns415UnsupportedMediaTypeAsJson()
    {
        var response = await _client.PostAsync(
            "/change-requests", new StringContent("title=abc", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("unsupported-media-type", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task UnexpectedFailureInsideAnOperation_Returns500GenericJsonWithNoDetail()
    {
        const string leaked = @"C:\secret\build\Handler.cs failed with token-sentinel-91c2";
        using var factory = new ApiFactory(
            configureServices: services =>
                services.AddSingleton<IChangeRequestService>(new ThrowingChangeRequestService(leaked)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/change-requests/00000000-0000-0000-0000-000000000001");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("unexpected-error", text);
        Assert.DoesNotContain("token-sentinel-91c2", text);
        Assert.DoesNotContain(@"C:\secret", text);
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain(factory.Logs.Lines, line => line.Contains("token-sentinel-91c2"));
        Assert.DoesNotContain(factory.Logs.Lines, line => line.Contains(@"C:\secret"));
    }

    [Fact(Skip = "synthetic")]
    public async Task EveryApiResponse_IsJson_AndCarriesNoStackTraceOrPath()
    {
        var id = await _client.SubmitAsync();
        var missing = "00000000-0000-0000-0000-000000000001";
        var requests = new List<Func<Task<HttpResponseMessage>>>
        {
            () => _client.GetAsync($"/change-requests/{id}"),
            () => _client.GetAsync($"/change-requests/{id}/audit"),
            () => _client.GetAsync($"/change-requests/{id}/clearance"),
            () => _client.PostAsync($"/change-requests/{id}/drafts", null),
            () => _client.GetAsync($"/change-requests/{id}/drafts/latest"),
            () => _client.GetAsync($"/change-requests/{id}/drafts"),
            () => _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()),
            () => _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()),
            () => _client.GetAsync($"/change-requests/{missing}"),
            () => _client.GetAsync("/change-requests/not-a-guid"),
            () => _client.PostRawAsync("/change-requests", "{"),
            () => _client.GetAsync("/nope"),
            () => _client.DeleteAsync("/change-requests")
        };

        foreach (var send in requests)
        {
            var response = await send();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Exception", text);
            Assert.DoesNotContain("StackTrace", text);
            Assert.DoesNotContain(" at ", text);
            Assert.DoesNotContain(@":\", text);
        }
    }

    private sealed class ThrowingChangeRequestService : IChangeRequestService
    {
        private readonly string _message;

        public ThrowingChangeRequestService(string message) => _message = message;

        public SubmissionResult Submit(SubmissionRequest request) => throw new InvalidOperationException(_message);

        public ChangeRequest? GetById(Guid changeRequestId) => throw new InvalidOperationException(_message);

        public IReadOnlyList<ChangeRequestAuditEntry>? GetAuditHistory(Guid changeRequestId) =>
            throw new InvalidOperationException(_message);
    }
}
