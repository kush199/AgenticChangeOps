using System.Net;
using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

public class DescriptionTests : IDisposable
{
    private const string DescriptionPath = "/openapi/v1.json";

    private readonly ApiFactory _factory = new(settings: new Dictionary<string, string?>
    {
        [AzureOpenAIGeneratorOptions.EndpointVariable] = "https://sentinel-endpoint.invalid/",
        [AzureOpenAIGeneratorOptions.DeploymentVariable] = "sentinel-deployment"
    });

    private readonly HttpClient _client;

    public DescriptionTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Description_IsServedAsJson_AndListsExactlyTheSeventeenRoutes()
    {
        var response = await _client.GetAsync(DescriptionPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        var operations = json.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(m => m.Name is "get" or "post" or "put" or "delete" or "patch")
                .Select(m => $"{m.Name.ToUpperInvariant()} {path.Name}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var expected = new[]
        {
            "GET /change-requests/{id}",
            "GET /change-requests/{id}/audit",
            "GET /change-requests/{id}/clearance",
            "GET /change-requests/{id}/drafts",
            "GET /change-requests/{id}/drafts/latest",
            "GET /change-requests/{id}/designs",
            "GET /change-requests/{id}/designs/latest",
            "GET /change-requests/{id}/proposals",
            "GET /change-requests/{id}/proposals/latest",
            "GET /change-requests/{id}/workflow",
            "GET /operator/automatic-advancement",
            "POST /change-requests",
            "POST /change-requests/{id}/decisions",
            "POST /change-requests/{id}/designs",
            "POST /change-requests/{id}/drafts",
            "POST /change-requests/{id}/proposals",
            "POST /change-requests/{id}/workflow/advance"
        }.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, operations);
    }

    [Fact]
    public async Task Description_DocumentsEveryErrorCode()
    {
        var text = await (await _client.GetAsync(DescriptionPath)).Content.ReadAsStringAsync();

        foreach (var code in new[]
        {
            "invalid-body", "validation-failed", "invalid-identifier", "not-found", "no-draft",
            "route-not-found", "method-not-allowed", "conflict", "body-too-large",
            "generation-failed", "drafting-not-configured", "unexpected-error", "no-step-due"
        })
        {
            Assert.Contains(code, text);
        }
    }

    [Fact]
    public async Task Description_ContainsNoConfigurationValue()
    {
        var text = await (await _client.GetAsync(DescriptionPath)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("sentinel-endpoint", text);
        Assert.DoesNotContain("sentinel-deployment", text);
        Assert.DoesNotContain(AzureOpenAIGeneratorOptions.EndpointVariable, text);
        Assert.DoesNotContain(AzureOpenAIGeneratorOptions.DeploymentVariable, text);
    }

    [Fact]
    public async Task Description_ParsesAsJson_AndContainsNoEmailAddressesOrExternalUrls()
    {
        var text = await (await _client.GetAsync(DescriptionPath)).Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.DoesNotContain("@", text);
        Assert.DoesNotContain("https://", text);
    }

    [Fact]
    public async Task BrowsablePage_IsServed()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
