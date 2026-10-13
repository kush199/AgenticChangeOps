using System.Net;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

public class DesignFailureTests
{
    private const string Sentinel = "sentinel-secret-text-7f3a";

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task GenerationFailure_Returns502CarryingOnlyTheCategory(GenerationFailureCategory category)
    {
        using var host = new DesignHost(FakeDesignGenerator.ThrowsCategory(category));
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", json.GetProperty("code").GetString());
        Assert.Equal(category.ToString(), json.GetProperty("category").GetString());
        var members = json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "category", "code", "message" }, members);
        Assert.Contains("design", json.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerationFailure_StoresNothingAndLeavesTheRequestApproved()
    {
        using var host = new DesignHost(FakeDesignGenerator.ThrowsCategory(GenerationFailureCategory.TimedOut));
        var id = await host.ApprovedWithBasisAsync();

        await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        var history = await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
        Assert.Equal(new[] { "Submitted", "Approved" }, await host.AuditEventTypesAsync(id));
        var request = await (await host.Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Approved", request.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("components")]
    [InlineData("risks")]
    [InlineData("rollback")]
    public async Task IncompleteContent_Returns502MalformedOutputAndStoresNothing(string blank)
    {
        var content = FakeDesignGenerator.SyntheticContent();
        content = blank switch
        {
            "summary" => content with { ApproachSummary = "   " },
            "components" => content with { AffectedComponents = Array.Empty<string>() },
            "risks" => content with { ImpactAndRisks = Array.Empty<string>() },
            _ => content with { RollbackConsiderations = Array.Empty<string>() }
        };
        using var host = new DesignHost(FakeDesignGenerator.ReturnsContent(content));
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", json.GetProperty("code").GetString());
        Assert.Equal("MalformedOutput", json.GetProperty("category").GetString());
        Assert.Equal(new[] { "Submitted", "Approved" }, await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task NullContent_Returns502MalformedOutput()
    {
        using var host = new DesignHost(FakeDesignGenerator.ReturnsContent(null));
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MalformedOutput", (await response.ReadJsonAsync()).GetProperty("category").GetString());
    }

    [Fact]
    public async Task ArbitraryGeneratorException_Returns500AndLeaksNothing()
    {
        using var host = new DesignHost(FakeDesignGenerator.ThrowsArbitrary(new InvalidOperationException(Sentinel)));
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("unexpected-error", text);
        Assert.DoesNotContain(Sentinel, text);
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain(host.Logs.Lines, line => line.Contains(Sentinel));
    }

    [Fact]
    public async Task ModelTextInUnusableContent_IsNeverInTheResponseOrLogs()
    {
        var content = FakeDesignGenerator.SyntheticContent() with
        {
            ApproachSummary = Sentinel,
            RollbackConsiderations = Array.Empty<string>()
        };
        using var host = new DesignHost(FakeDesignGenerator.ReturnsContent(content));
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Sentinel, text);
        Assert.DoesNotContain(host.Logs.Lines, line => line.Contains(Sentinel));
    }

    [Fact]
    public async Task NotConfigured_Returns503DesignNotConfigured_AndGeneratorIsNeverCalled()
    {
        using var host = new DesignHost(configured: false);
        var id = await host.ApprovedWithBasisAsync();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("design-not-configured", json.GetProperty("code").GetString());
        Assert.Equal(0, host.Generator.CallCount);
        Assert.Equal(new[] { "Submitted", "Approved" }, await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task NotConfigured_InvalidIdentifierStillReturns400_AndRetrievalStillWorks()
    {
        using var host = new DesignHost(configured: false);
        var id = await host.ApprovedWithBasisAsync();

        var invalid = await host.Client.PostAsync("/change-requests/not-a-guid/designs", null);
        var latest = await host.Client.GetAsync($"/change-requests/{id}/designs/latest");
        var history = await host.Client.GetAsync($"/change-requests/{id}/designs");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, latest.StatusCode);
        Assert.Equal("no-design-draft", (await latest.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
    }

    // ---- "same error conventions as the requirements drafting operations" ----

    [Fact]
    public async Task Parity_GenerationFailureHasTheSameStatusCodeAndShapeAsRequirementsDrafting()
    {
        var category = GenerationFailureCategory.Unreachable;
        using var host = new DesignHost(FakeDesignGenerator.ThrowsCategory(category));
        var designId = await host.ApprovedWithBasisAsync();
        using var requirementsFactory = new ApiFactory(
            FakeDraftGenerator.ThrowsCategory(category), draftingConfigured: true);
        using var requirementsClient = requirementsFactory.CreateClient();
        var draftId = await requirementsClient.SubmitAsync();

        var design = await host.Client.PostAsync($"/change-requests/{designId}/designs", null);
        var draft = await requirementsClient.PostAsync($"/change-requests/{draftId}/drafts", null);

        Assert.Equal(draft.StatusCode, design.StatusCode);
        var designJson = await design.ReadJsonAsync();
        var draftJson = await draft.ReadJsonAsync();
        Assert.Equal(draftJson.GetProperty("code").GetString(), designJson.GetProperty("code").GetString());
        Assert.Equal(draftJson.GetProperty("category").GetString(), designJson.GetProperty("category").GetString());
        Assert.Equal(
            draftJson.EnumerateObject().Select(p => p.Name).OrderBy(n => n),
            designJson.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Parity_StateRefusalHasTheSameStatusCodeAndCodeAsRequirementsDrafting()
    {
        using var host = new DesignHost();
        var submitted = await host.Client.SubmitAsync();
        host.Basis.Set(submitted, FakeBasisSource.Basis());
        var approved = await host.ApprovedWithBasisAsync();

        var design = await host.Client.PostAsync($"/change-requests/{submitted}/designs", null);
        var draft = await host.Client.PostAsync($"/change-requests/{approved}/drafts", null);

        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        Assert.Equal(draft.StatusCode, design.StatusCode);
        Assert.Equal(
            (await draft.ReadJsonAsync()).GetProperty("code").GetString(),
            (await design.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Parity_NotConfiguredHasTheSameStatusCodeAndShapeAsRequirementsDrafting()
    {
        using var host = new DesignHost(configured: false);
        var designId = await host.ApprovedWithBasisAsync();
        using var requirementsFactory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: false);
        using var requirementsClient = requirementsFactory.CreateClient();
        var draftId = await requirementsClient.SubmitAsync();

        var design = await host.Client.PostAsync($"/change-requests/{designId}/designs", null);
        var draft = await requirementsClient.PostAsync($"/change-requests/{draftId}/drafts", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, draft.StatusCode);
        Assert.Equal(draft.StatusCode, design.StatusCode);
        Assert.Equal(
            (await draft.ReadJsonAsync()).EnumerateObject().Select(p => p.Name).OrderBy(n => n),
            (await design.ReadJsonAsync()).EnumerateObject().Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Parity_NothingYetHasTheSameStatusCodeAndShapeAsRequirementsDrafting()
    {
        using var host = new DesignHost();
        var id = await host.ApprovedWithBasisAsync();

        var noDesign = await host.Client.GetAsync($"/change-requests/{id}/designs/latest");
        var noDraft = await host.Client.GetAsync($"/change-requests/{id}/drafts/latest");
        var emptyDesigns = await host.Client.GetAsync($"/change-requests/{id}/designs");
        var emptyDrafts = await host.Client.GetAsync($"/change-requests/{id}/drafts");

        Assert.Equal(HttpStatusCode.NotFound, noDraft.StatusCode);
        Assert.Equal(noDraft.StatusCode, noDesign.StatusCode);
        Assert.Equal(
            (await noDraft.ReadJsonAsync()).EnumerateObject().Select(p => p.Name).OrderBy(n => n),
            (await noDesign.ReadJsonAsync()).EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal(emptyDrafts.StatusCode, emptyDesigns.StatusCode);
        Assert.Empty((await emptyDesigns.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Parity_UnknownAndInvalidIdentifiersMatchRequirementsDrafting()
    {
        using var host = new DesignHost();
        const string unknown = "00000000-0000-0000-0000-000000000001";

        var designUnknown = await host.Client.PostAsync($"/change-requests/{unknown}/designs", null);
        var draftUnknown = await host.Client.PostAsync($"/change-requests/{unknown}/drafts", null);
        var designInvalid = await host.Client.PostAsync("/change-requests/not-a-guid/designs", null);
        var draftInvalid = await host.Client.PostAsync("/change-requests/not-a-guid/drafts", null);

        Assert.Equal(draftUnknown.StatusCode, designUnknown.StatusCode);
        Assert.Equal(draftInvalid.StatusCode, designInvalid.StatusCode);
        Assert.Equal(
            (await draftUnknown.ReadJsonAsync()).GetProperty("code").GetString(),
            (await designUnknown.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(
            (await draftInvalid.ReadJsonAsync()).GetProperty("code").GetString(),
            (await designInvalid.ReadJsonAsync()).GetProperty("code").GetString());
    }
}
