using System.Net;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Baseline;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>contracts/settings-and-failures.md: defaults, validation, lazy reading, and no value ever in a message or a log.</summary>
public sealed class ReviewSettingsTests
{
    private static ReviewSettings Read(Dictionary<string, string?> values) =>
        ReviewSettings.FromConfiguration(name => values.TryGetValue(name, out var v) ? v : null);

    [Fact]
    public void TheVariableNames_AreTheDocumentedOnes()
    {
        Assert.Equal("AGENTICCHANGEOPS_REVIEW_MAX_FINDINGS", ReviewSettings.MaxFindingsVariable);
        Assert.Equal("AGENTICCHANGEOPS_REVIEW_MAX_BYTES", ReviewSettings.MaxBytesVariable);
    }

    [Fact]
    public void WithNothingSet_TheDefaultsApply()
    {
        var settings = Read(new());

        Assert.Equal(8, settings.Limits.MaxFindings);
        Assert.Equal(8000, settings.Limits.MaxTotalBytes);
        Assert.Equal(ReviewLimits.Defaults, settings.Limits);
    }

    [Fact]
    public void ValidValues_AreUsed()
    {
        var settings = Read(new()
        {
            [ReviewSettings.MaxFindingsVariable] = " 3 ",
            [ReviewSettings.MaxBytesVariable] = "1200"
        });

        Assert.Equal(new ReviewLimits(3, 1200), settings.Limits);
    }

    [Theory]
    [InlineData(ReviewSettings.MaxFindingsVariable, "0")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "-4")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "+4")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "2.5")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "1e3")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "many")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "")]
    [InlineData(ReviewSettings.MaxFindingsVariable, "99999999999999999999")]
    [InlineData(ReviewSettings.MaxBytesVariable, "0")]
    [InlineData(ReviewSettings.MaxBytesVariable, "-1")]
    [InlineData(ReviewSettings.MaxBytesVariable, "8,000")]
    [InlineData(ReviewSettings.MaxBytesVariable, "lots")]
    public void AnInvalidValue_IsReported_ByNameOnly(string name, string value)
    {
        var ex = Assert.Throws<GeneratorConfigurationException>(() => Read(new() { [name] = value }));

        Assert.Contains(name, ex.Message);
        if (value.Length > 1)
            Assert.DoesNotContain(value, ex.Message.Replace(name, string.Empty));
    }

    [Fact]
    public void TheFirstWrongSetting_IsTheOneNamed()
    {
        var ex = Assert.Throws<GeneratorConfigurationException>(() => Read(new()
        {
            [ReviewSettings.MaxFindingsVariable] = "0",
            [ReviewSettings.MaxBytesVariable] = "0"
        }));

        Assert.Contains(ReviewSettings.MaxFindingsVariable, ex.Message);
    }

    // ---- lazy reading ----------------------------------------------------------------

    private static readonly Dictionary<string, string?> Garbage = new()
    {
        [ReviewSettings.MaxFindingsVariable] = "lots-of-garbage",
        [ReviewSettings.MaxBytesVariable] = "-9"
    };

    [Fact]
    public async Task AHostWithGarbageReviewSettings_StartsAndServesEveryExistingRouteIdentically()
    {
        using var host = new ReviewHost(settings: new(Garbage));

        var response = await host.Client.GetAsync("/openapi/v1.json");
        var id = await host.ReadyForReviewAsync();
        var workflow = await host.Client.GetAsync($"/change-requests/{id}/workflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, workflow.StatusCode);
        Assert.DoesNotContain(host.Logs.Lines, line => line.Contains("Review", StringComparison.OrdinalIgnoreCase)
            && line.Contains("not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheApiDescription_IsTheRecordedOne_EvenWithGarbageReviewSettings()
    {
        using var host = new ReviewHost(settings: new(Garbage));

        var response = await host.Client.GetAsync("/openapi/v1.json");

        var text = await response.Content.ReadAsStringAsync();
        GoldenFile.Verify("openapi.json", text.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task TheSettingsAreRead_WhenTheServiceIsFirstResolved_AndAnInvalidOneMakesReviewsNotConfigured()
    {
        using var host = new ReviewHost(settings: new(Garbage));
        var id = await host.ReadyForReviewAsync();

        var service = host.Reviewing; // resolving does not throw
        var result = await service.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.Unreachable, result.FailureCategory);
        Assert.Equal(0, host.ReviewClient.CallCount); // refused before any model call
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
    }

    [Fact]
    public async Task TheNotConfiguredWarning_IsLoggedOnce_AndNamesNoSettingAndNoValue()
    {
        using var host = new ReviewHost(settings: new(Garbage));
        var id = await host.ReadyForReviewAsync();

        await host.Reviewing.ReviewAsync(id);
        await host.Reviewing.ReviewAsync(id);

        var warnings = host.Logs.Lines
            .Where(l => l.Contains("review", StringComparison.OrdinalIgnoreCase) && l.Contains("not configured", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(warnings);
        Assert.DoesNotContain(ReviewSettings.MaxFindingsVariable, warnings[0]);
        Assert.DoesNotContain("lots-of-garbage", warnings[0]);
        Assert.DoesNotContain("-9", warnings[0]);
    }

    [Fact]
    public async Task InvalidModelSettings_AlsoMakeReviewsNotConfigured_WithoutNamingThem()
    {
        using var host = new ReviewHost(modelConfigured: false);
        var id = await host.ReadyForProposalAsync();
        host.SeedProposal(id);

        var result = await host.Reviewing.ReviewAsync(id);

        Assert.Equal(GenerationFailureCategory.Unreachable, result.FailureCategory);
        Assert.Equal(0, host.ReviewClient.CallCount);
        var logs = string.Join("\n", host.Logs.Lines);
        Assert.DoesNotContain("AGENTICCHANGEOPS_MODEL_ENDPOINT", logs);
        Assert.DoesNotContain(ReviewHost.Endpoint, logs);
    }
}
