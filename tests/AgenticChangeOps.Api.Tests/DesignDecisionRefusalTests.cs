using System.Net;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

public sealed class DesignDecisionRefusalTests : IDisposable
{
    private readonly DesignHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task AssertNothingStoredAsync(Guid id, int decisions = 0)
    {
        var store = _host.Services.GetRequiredService<IChangeRequestStore>();
        Assert.Equal(decisions, store.GetAgentOutputs<DesignDecision>(id).Count);
        Assert.Equal(decisions, (await _host.AuditEventTypesAsync(id)).Count(e => e == "DesignDecided"));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    [InlineData("%20")]
    public async Task AMalformedId_Is400InvalidIdentifier(string id)
    {
        var response = await _host.Client.PostJsonAsync(
            $"/change-requests/{id}/designs/1/decisions", DesignDecisionHttp.Body("Accept"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-identifier", await response.ErrorCodeAsync());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    [InlineData("latest")]
    public async Task AMalformedVersion_Is400ValidationFailed_NamingTheVersion(string version)
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, version, DesignDecisionHttp.Body("Accept"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        Assert.Contains("DesignVersion", json.FieldNames());
        await AssertNothingStoredAsync(id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task ABodyThatIsNotAJsonObject_IsTheExistingInvalidBodyError(string raw)
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.Client.PostRawAsync($"/change-requests/{id}/designs/1/decisions", raw);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-body", await response.ErrorCodeAsync());
        await AssertNothingStoredAsync(id);
    }

    [Fact]
    public async Task AnUnknownChangeRequest_Is404NotFound()
    {
        var response = await _host.DecideAsync(Guid.NewGuid(), 1, "Accept");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task ARequestThatIsNotApproved_Is409Conflict()
    {
        var id = await _host.Client.SubmitAsync();

        var response = await _host.DecideAsync(id, 1, "Accept");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict", await response.ErrorCodeAsync());
        Assert.Contains("Status", (await response.ReadJsonAsync()).FieldNames());
    }

    [Fact]
    public async Task AVersionThatDoesNotExist_Is404DesignVersionNotFound()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, 5, "Accept");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("design-version-not-found", await response.ErrorCodeAsync());
        await AssertNothingStoredAsync(id);
    }

    [Fact]
    public async Task AnOlderVersion_Is409DesignOutdated()
    {
        var id = await _host.ApprovedWithDesignsAsync(designs: 2);

        var response = await _host.DecideAsync(id, 1, "Accept");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("design-outdated", await response.ErrorCodeAsync());
        await AssertNothingStoredAsync(id);
    }

    [Fact]
    public async Task ADesignBasedOnOlderRequirements_Is409DesignOutdated()
    {
        var id = await _host.ApprovedWithDesignsAsync(basisVersion: 1);
        _host.Basis.Set(id, FakeBasisSource.Basis(2));

        var response = await _host.DecideAsync(id, 1, "Accept");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("design-outdated", await response.ErrorCodeAsync());
        await AssertNothingStoredAsync(id);
    }

    [Fact]
    public async Task ADecidedVersion_Is409DesignAlreadyDecided_AndTheFirstDecisionStands()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        (await _host.DecideAsync(id, 1, "Accept")).EnsureSuccessStatusCode();

        var response = await _host.DecideAsync(id, 1, "Reject", reason: "Changed my mind.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("design-already-decided", await response.ErrorCodeAsync());
        await AssertNothingStoredAsync(id, decisions: 1);
        var latest = await (await _host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        Assert.Equal("Accepted", latest.GetProperty("decision").GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task AnUnrecognisedDecision_ListsTheAcceptedValues_AndNeverEchoesTheInput()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, 1, "Defer-sentinel-value");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accept, Reject", text);
        Assert.DoesNotContain("sentinel", text);
        await AssertNothingStoredAsync(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public async Task AMissingOrNumericDecision_IsRefusedAsAnInvalidDecision(string? value)
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, "1", new { approverName = "Jordan Reed", decision = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Decision", (await response.ReadJsonAsync()).FieldNames());
    }

    [Fact]
    public async Task EveryFailingField_IsListed()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, "1", new { approverName = "  ", decision = "Reject" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("validation-failed", json.GetProperty("code").GetString());
        Assert.Equal(new[] { "ApproverName", "Reason" }, json.FieldNames().OrderBy(x => x));
        await AssertNothingStoredAsync(id);
    }

    [Theory]
    [InlineData(101, 0, "ApproverName")]
    [InlineData(5, 1001, "Reason")]
    public async Task TooLongText_IsRefusedUnderTheApprovalDecisionLimits(int nameLength, int reasonLength, string field)
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(
            id, 1, "Reject", name: new string('n', nameLength), reason: reasonLength == 0 ? "ok" : new string('r', reasonLength));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.ReadJsonAsync()).FieldNames());
        await AssertNothingStoredAsync(id);
    }

    [Fact]
    public async Task TheChecksRunInTheDocumentedOrder_StateRefusalsBeforeFieldErrors()
    {
        var id = await _host.ApprovedWithDesignsAsync(designs: 2);

        var response = await _host.DecideAsync(id, "1", new { approverName = " ", decision = "nonsense" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("design-outdated", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task AStorageFailureWhileSavingTheDecision_Is503StorageUnavailable_AndStoresAndLeaksNothing()
    {
        var failing = new DesignDecisionSaveFailingStore(
            new StorageUnavailableException(StorageFailureCategory.UnsupportedLayout));
        using var host = new DesignHost(store: failing);
        var id = await host.ApprovedWithDesignsAsync();

        var response = await host.DecideAsync(id, 1, "Accept", name: "name-sentinel-5e0a");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("storage-unavailable", json.GetProperty("code").GetString());
        Assert.Equal("UnsupportedLayout", json.GetProperty("category").GetString());
        var text = json.GetRawText();
        Assert.DoesNotContain("name-sentinel", text);
        Assert.DoesNotContain("disk", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(failing.GetAgentOutputs<DesignDecision>(id));
        Assert.Equal(0, (await host.AuditEventTypesAsync(id)).Count(e => e == "DesignDecided"));
        Assert.DoesNotContain(host.Logs.Lines, l => l.Contains("name-sentinel"));
    }
}
