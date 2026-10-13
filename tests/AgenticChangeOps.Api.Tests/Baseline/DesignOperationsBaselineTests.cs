using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests.Baseline;

/// <summary>
/// Convergence test for slice 009 (FR-017). Records, as responses, the design operations that
/// do not depend on what the model answers: unknown and invalid identifiers, a request that is
/// not Approved, an Approved request with no requirements draft, the not-configured state, and
/// latest and history before and after designs exist. The golden was captured from the code as
/// it stood before the shared plumbing was extracted (commit a49108f) and is never rewritten
/// from later code. Rewrite only with <see cref="ApiBaselineTests.UpdateVariable"/> = 1 on
/// reviewed, intended change.
/// </summary>
public class DesignOperationsBaselineTests
{
    [Fact]
    public async Task DesignOperationsOutsideTheModel_ReturnTheRecordedResponses()
    {
        var log = new TranscriptBuilder();

        await RecordMainFlowAsync(log);
        await RecordNotConfiguredAsync(log);

        GoldenFile.Verify("design-operations-transcript.txt", log.ToString());
    }

    private static async Task RecordMainFlowAsync(TranscriptBuilder log)
    {
        using var host = new DesignHost();
        var client = host.Client;

        // Identifiers
        var unknown = Guid.NewGuid();
        await log.RecordAsync("design unknown id", await client.PostAsync($"/change-requests/{unknown}/designs", null));
        await log.RecordAsync("latest design unknown id", await client.GetAsync($"/change-requests/{unknown}/designs/latest"));
        await log.RecordAsync("design history unknown id", await client.GetAsync($"/change-requests/{unknown}/designs"));
        await log.RecordAsync("design invalid id", await client.PostAsync("/change-requests/not-a-guid/designs", null));
        await log.RecordAsync("latest design invalid id", await client.GetAsync("/change-requests/not-a-guid/designs/latest"));
        await log.RecordAsync("design history invalid id", await client.GetAsync("/change-requests/not-a-guid/designs"));

        // Not Approved: Submitted, Deferred, Rejected (a requirements draft exists in each)
        var submitted = await client.SubmitAsync();
        host.Basis.Set(submitted, FakeBasisSource.Basis());
        await log.RecordAsync("design submitted request", await client.PostAsync($"/change-requests/{submitted}/designs", null));

        var deferred = await client.SubmitAsync();
        host.Basis.Set(deferred, FakeBasisSource.Basis());
        (await client.PostJsonAsync($"/change-requests/{deferred}/decisions", SyntheticData.Defer()))
            .EnsureSuccessStatusCode();
        await log.RecordAsync("design deferred request", await client.PostAsync($"/change-requests/{deferred}/designs", null));

        var rejected = await client.SubmitAsync();
        host.Basis.Set(rejected, FakeBasisSource.Basis());
        (await client.PostJsonAsync($"/change-requests/{rejected}/decisions",
            new DecisionBody("Sam Sample", "Reject", "Out of scope for this quarter."))).EnsureSuccessStatusCode();
        await log.RecordAsync("design rejected request", await client.PostAsync($"/change-requests/{rejected}/designs", null));

        // Approved without a requirements draft
        var noBasis = await host.ApprovedWithoutBasisAsync();
        await log.RecordAsync("design approved no requirements draft",
            await client.PostAsync($"/change-requests/{noBasis}/designs", null));
        await log.RecordAsync("latest design after refusal", await client.GetAsync($"/change-requests/{noBasis}/designs/latest"));
        await log.RecordAsync("audit after refusal", await client.GetAsync($"/change-requests/{noBasis}/audit"));

        // Approved with a requirements draft: before any design, then two designs
        var approved = await host.ApprovedWithBasisAsync();
        await log.RecordAsync("latest design before any", await client.GetAsync($"/change-requests/{approved}/designs/latest"));
        await log.RecordAsync("design history before any", await client.GetAsync($"/change-requests/{approved}/designs"));
        await log.RecordAsync("design first", await client.PostAsync($"/change-requests/{approved}/designs", null));
        await log.RecordAsync("design second", await client.PostAsync($"/change-requests/{approved}/designs", null));
        await log.RecordAsync("latest design", await client.GetAsync($"/change-requests/{approved}/designs/latest"));
        await log.RecordAsync("design history", await client.GetAsync($"/change-requests/{approved}/designs"));
        await log.RecordAsync("audit after designing", await client.GetAsync($"/change-requests/{approved}/audit"));
    }

    private static async Task RecordNotConfiguredAsync(TranscriptBuilder log)
    {
        using var host = new DesignHost(configured: false);
        var client = host.Client;

        var id = await host.ApprovedWithBasisAsync();

        await log.RecordAsync("not configured design", await client.PostAsync($"/change-requests/{id}/designs", null));
        await log.RecordAsync("not configured design invalid id",
            await client.PostAsync("/change-requests/not-a-guid/designs", null));
        await log.RecordAsync("not configured latest design", await client.GetAsync($"/change-requests/{id}/designs/latest"));
        await log.RecordAsync("not configured design history", await client.GetAsync($"/change-requests/{id}/designs"));
    }
}
