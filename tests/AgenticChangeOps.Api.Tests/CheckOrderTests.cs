using System.Net;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// On the requirements and design drafting operations the checks run in a fixed order: malformed
/// identifier, then unknown identifier, then "not configured", then the existing rules.
/// </summary>
public class CheckOrderTests
{
    public static IEnumerable<object[]> Operations() => new[]
    {
        new object[] { "drafts", "drafting-not-configured" },
        new object[] { "designs", "design-not-configured" },
    };

    private sealed class Host : IDisposable
    {
        private readonly ApiFactory _factory;

        public Host(bool configured)
        {
            Generator = FakeDraftGenerator.ReturnsSyntheticContent();
            DesignGenerator = FakeDesignGenerator.ReturnsSyntheticContent();
            _factory = new ApiFactory(Generator, draftingConfigured: configured, configureServices: services =>
            {
                services.AddSingleton<IDesignDraftGenerator>(DesignGenerator);
                services.AddSingleton(new DesignAvailability(configured));
            });
            Client = _factory.CreateClient();
        }

        public FakeDraftGenerator Generator { get; }
        public FakeDesignGenerator DesignGenerator { get; }
        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }

    private static async Task<(HttpStatusCode Status, string Body)> PostAsync(HttpClient client, string path)
    {
        var response = await client.PostAsync(path, null);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task NotConfigured_MalformedIdentifier_IsInvalidIdentifier(string operation, string _)
    {
        using var host = new Host(configured: false);

        var (status, body) = await PostAsync(host.Client, $"/change-requests/not-a-guid/{operation}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("invalid-identifier", body);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task NotConfigured_UnknownIdentifier_IsNotFound_NotNotConfigured(string operation, string notConfiguredCode)
    {
        using var host = new Host(configured: false);

        var (status, body) = await PostAsync(host.Client, $"/change-requests/{Guid.NewGuid()}/{operation}");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Contains("\"code\":\"not-found\"", body.Replace(" ", ""));
        Assert.DoesNotContain(notConfiguredCode, body);
        Assert.Equal(0, host.Generator.CallCount);
        Assert.Equal(0, host.DesignGenerator.CallCount);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task UnknownIdentifier_GivesTheSameResponse_WhetherOrNotConfigured(string operation, string _)
    {
        var unknown = Guid.NewGuid();
        using var configured = new Host(configured: true);
        using var unconfigured = new Host(configured: false);

        var withModel = await configured.Client.PostAsync($"/change-requests/{unknown}/{operation}", null);
        var withoutModel = await unconfigured.Client.PostAsync($"/change-requests/{unknown}/{operation}", null);

        Assert.Equal(HttpStatusCode.NotFound, withModel.StatusCode);
        Assert.Equal(withModel.StatusCode, withoutModel.StatusCode);
        Assert.Equal(
            await withModel.Content.ReadAsStringAsync(), await withoutModel.Content.ReadAsStringAsync());
        Assert.Equal(
            withModel.Content.Headers.ContentType?.ToString(), withoutModel.Content.Headers.ContentType?.ToString());
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task NotConfigured_KnownIdentifier_IsStillNotConfigured(string operation, string notConfiguredCode)
    {
        using var host = new Host(configured: false);
        var id = await host.Client.SubmitAsync();

        var (status, body) = await PostAsync(host.Client, $"/change-requests/{id}/{operation}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains(notConfiguredCode, body);
        Assert.Equal(0, host.Generator.CallCount);
        Assert.Equal(0, host.DesignGenerator.CallCount);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Configured_KnownAndMalformedIdentifiers_BehaveAsBefore(string operation, string _)
    {
        using var host = new Host(configured: true);
        var id = await host.Client.SubmitAsync();

        var malformed = await PostAsync(host.Client, $"/change-requests/not-a-guid/{operation}");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.Status);
        Assert.Contains("invalid-identifier", malformed.Body);

        var known = await PostAsync(host.Client, $"/change-requests/{id}/{operation}");
        // Drafts are accepted for a submitted request; a design needs an approved one.
        Assert.Equal(
            operation == "drafts" ? HttpStatusCode.Created : HttpStatusCode.Conflict,
            known.Status);
    }
}
