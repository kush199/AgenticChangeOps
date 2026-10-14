using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class WorkflowNoLeakTests
{
    private const string Sentinel = "sentinel-secret-text-7f3a https://sentinel-endpoint.invalid/ sentinel-deployment";

    [Fact]
    public async Task RequirementsStepThrowingArbitraryText_LeaksNothingInResponseOrLogs()
    {
        var requirements = FakeDraftGenerator.ThrowsArbitrary(new InvalidOperationException(Sentinel));
        using var factory = new ApiFactory(requirements, draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        var response = await client.PostAsync($"/change-requests/{id}/workflow/advance", null);
        var text = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("sentinel", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.Logs.Lines, l => l.Contains("sentinel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DesignStepThrowingArbitraryText_LeaksNothingInResponseOrLogs()
    {
        using var host = new DesignHost(
            FakeDesignGenerator.ThrowsArbitrary(new InvalidOperationException(Sentinel)), useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        await host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();

        var response = await host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);
        var text = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("sentinel", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(host.Logs.Lines, l => l.Contains("sentinel", StringComparison.OrdinalIgnoreCase));
    }
}
