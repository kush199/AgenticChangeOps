using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.Configuration;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Slice 021: the review offer follows the model and review settings on every read, treats invalid
/// settings as "not available" without an error or a log line, and works over the durable store.
/// </summary>
public sealed class ReviewOfferSettingsTests
{
    private static readonly Dictionary<string, string?> Garbage = new()
    {
        [ReviewSettings.MaxFindingsVariable] = "lots-of-garbage",
        [ReviewSettings.MaxBytesVariable] = "-9"
    };

    private static bool Offered(HttpResponseMessage response, out string? step)
    {
        var json = response.ReadJsonAsync().GetAwaiter().GetResult();
        var present = json.TryGetProperty("offeredStep", out var value);
        step = present ? value.GetString() : null;
        return present;
    }

    [Fact]
    public async Task InvalidReviewSettings_GiveNoReviewOffer_AndNoErrorOrLogLine()
    {
        using var host = new ReviewHost(settings: new(Garbage));
        var id = await host.ReadyForReviewAsync();

        var response = await host.Client.GetAsync($"/change-requests/{id}/workflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(Offered(response, out _));
        Assert.DoesNotContain(host.Logs.Lines, line => line.Contains("Review", StringComparison.OrdinalIgnoreCase)
            && line.Contains("not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheOffer_FollowsTheSettingsOnEveryAsk_AndRemembersNothing()
    {
        var source = new Dictionary<string, string?>
        {
            ["AGENTICCHANGEOPS_MODEL_ENDPOINT"] = ReviewHost.Endpoint,
            ["AGENTICCHANGEOPS_MODEL_DEPLOYMENT"] = ReviewHost.Deployment
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(source).Build();
        var offer = new ReviewOffer(configuration);

        Assert.True(offer.IsOffered);

        configuration[ReviewSettings.MaxBytesVariable] = "-9";
        Assert.False(offer.IsOffered);

        configuration[ReviewSettings.MaxBytesVariable] = null;
        Assert.True(offer.IsOffered);

        configuration["AGENTICCHANGEOPS_MODEL_DEPLOYMENT"] = null;
        Assert.False(offer.IsOffered);
    }

    [Fact]
    public async Task OnTheDurableStore_TheReviewIsOffered_ThenNotOnceReviewed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "changeops-offer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var settings = new Dictionary<string, string?>
            {
                [ApiFactory.StorageProviderKey] = "Sqlite",
                [ApiFactory.StoragePathKey] = Path.Combine(folder, "changeops.db")
            };
            using (var host = new ReviewHost(settings: settings))
            {
                var id = await host.ReadyForReviewAsync();

                Assert.True(Offered(await host.Client.GetAsync($"/change-requests/{id}/workflow"), out var before));
                Assert.Equal("Review", before);

                (await host.RequestReviewAsync(id)).EnsureSuccessStatusCode();

                Assert.False(Offered(await host.Client.GetAsync($"/change-requests/{id}/workflow"), out _));
            }
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
