using System.Net;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>Design generator that runs a callback mid-generation, then returns valid content.</summary>
internal sealed class HookedDesignGenerator : IDesignDraftGenerator
{
    public Func<Task>? OnGenerate { get; set; }

    public async Task<DesignContent?> GenerateAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)
    {
        if (OnGenerate is not null)
            await OnGenerate();
        return FakeDesignGenerator.SyntheticContent();
    }
}

public class WorkflowEndpointTests
{
    private static string Status(Guid id) => $"/change-requests/{id}/workflow";
    private static string Advance(Guid id) => $"/change-requests/{id}/workflow/advance";

    private static async Task Approve(DesignHost host, Guid id) =>
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();

    [Fact]
    public async Task Status_OfFreshRequest_NamesRequirementsDraftAsNext()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();

        var response = await host.Client.GetAsync(Status(id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(id, json.GetProperty("changeRequestId").GetGuid());
        Assert.Equal("Submitted", json.GetProperty("changeRequestStatus").GetString());
        Assert.Equal("AwaitingRequirementsDraft", json.GetProperty("stage").GetString());
        Assert.Equal("RequirementsDraft", json.GetProperty("nextStep").GetString());
        Assert.Equal(0, json.GetProperty("completedSteps").GetArrayLength());
    }

    [Fact]
    public async Task ApprovedWithoutRequirementsDraft_ShowsTheDraftAsDue_AndAdvanceProducesIt()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.ApprovedWithoutBasisAsync();

        var status = await (await host.Client.GetAsync(Status(id))).ReadJsonAsync();

        Assert.Equal("Approved", status.GetProperty("changeRequestStatus").GetString());
        Assert.Equal("AwaitingRequirementsDraft", status.GetProperty("stage").GetString());
        Assert.Equal("RequirementsDraft", status.GetProperty("nextStep").GetString());
        Assert.False(status.TryGetProperty("reason", out var reason) && reason.ValueKind != System.Text.Json.JsonValueKind.Null);

        var advanced = await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.Created, advanced.StatusCode);
        Assert.Equal($"/change-requests/{id}/drafts/latest", advanced.Headers.Location!.ToString());
        var body = await advanced.ReadJsonAsync();
        Assert.Equal("RequirementsDraft", body.GetProperty("step").GetString());
        Assert.Equal(1, body.GetProperty("draft").GetProperty("version").GetInt32());
        Assert.Equal("ReadyForDesign", body.GetProperty("status").GetProperty("stage").GetString());
        Assert.Equal("DesignDraft", body.GetProperty("status").GetProperty("nextStep").GetString());
        Assert.Equal(new[] { "Submitted", "Approved", "DraftGenerated" }, await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task Advance_RunsRequirementsThenRefusesUntilApproved_ThenRunsDesign()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();

        var first = await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal($"/change-requests/{id}/drafts/latest", first.Headers.Location!.ToString());
        var body = await first.ReadJsonAsync();
        Assert.Equal("RequirementsDraft", body.GetProperty("step").GetString());
        Assert.Equal(1, body.GetProperty("draft").GetProperty("version").GetInt32());
        Assert.Equal("AwaitingDecision", body.GetProperty("status").GetProperty("stage").GetString());
        var direct = await (await host.Client.GetAsync($"/change-requests/{id}/drafts/latest")).ReadJsonAsync();
        Assert.Equal(direct.ToString(), body.GetProperty("draft").ToString());

        var refused = await host.Client.PostAsync(Advance(id), null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var refusedJson = await refused.ReadJsonAsync();
        Assert.Equal("no-step-due", refusedJson.GetProperty("code").GetString());
        Assert.Equal(new[] { "Reason" }, refusedJson.FieldNames());
        Assert.Equal(
            "AwaitingDecision",
            refusedJson.GetProperty("errors")[0].GetProperty("message").GetString());

        await Approve(host, id);
        var second = await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal($"/change-requests/{id}/designs/latest", second.Headers.Location!.ToString());
        var designBody = await second.ReadJsonAsync();
        Assert.Equal("DesignDraft", designBody.GetProperty("step").GetString());
        var directDesign = await (await host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        Assert.Equal(directDesign.ToString(), designBody.GetProperty("draft").ToString());
        Assert.Equal("DesignComplete", designBody.GetProperty("status").GetProperty("stage").GetString());

        var done = await host.Client.PostAsync(Advance(id), null);
        Assert.Equal(HttpStatusCode.Conflict, done.StatusCode);
        Assert.Equal(
            new[] { "Submitted", "DraftGenerated", "Approved", "DesignGenerated" },
            await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task Refusal_StoresNothingAndCallsNoGenerator()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        await host.Client.PostAsync(Advance(id), null);
        var calls = host.Requirements.CallCount;

        await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(calls, host.Requirements.CallCount);
        Assert.Equal(0, host.Generator.CallCount);
        Assert.Equal(new[] { "Submitted", "DraftGenerated" }, await host.AuditEventTypesAsync(id));
    }

    [Theory]
    [InlineData("GET", "not-a-guid", "/workflow")]
    [InlineData("POST", "not-a-guid", "/workflow/advance")]
    public async Task MalformedId_Is400InvalidIdentifier(string method, string id, string suffix)
    {
        using var host = new DesignHost(useFakeBasis: false);
        var request = new HttpRequestMessage(new HttpMethod(method), $"/change-requests/{id}{suffix}");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-identifier", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("GET", "/workflow")]
    [InlineData("POST", "/workflow/advance")]
    public async Task UnknownId_Is404NotFound(string method, string suffix)
    {
        using var host = new DesignHost(useFakeBasis: false);
        var request = new HttpRequestMessage(
            new HttpMethod(method), $"/change-requests/{Guid.NewGuid()}{suffix}");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task DesignStepFailure_Is502WithOnlyTheCategory_AndStatusIsUnchanged(
        GenerationFailureCategory category)
    {
        using var host = new DesignHost(FakeDesignGenerator.ThrowsCategory(category), useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        await host.Client.PostAsync(Advance(id), null);
        await Approve(host, id);
        var before = await (await host.Client.GetAsync(Status(id))).ReadJsonAsync();

        var response = await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", json.GetProperty("code").GetString());
        Assert.Equal(category.ToString(), json.GetProperty("category").GetString());
        Assert.Equal(new[] { "category", "code", "message" }, json.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        var after = await (await host.Client.GetAsync(Status(id))).ReadJsonAsync();
        Assert.Equal(before.ToString(), after.ToString());
        Assert.DoesNotContain("DesignGenerated", await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task DueStepNotConfigured_Is503WithTheAgentsOwnCode()
    {
        using var host = new DesignHost(configured: false, useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        await host.Client.PostAsync(Advance(id), null);
        await Approve(host, id);

        var response = await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("design-not-configured", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(0, host.Generator.CallCount);
    }

    [Fact]
    public async Task NothingDue_WinsOverNotConfigured()
    {
        using var host = new DesignHost(configured: false, useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        await host.Client.PostAsync(Advance(id), null);

        var response = await host.Client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("no-step-due", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task DueRequirementsStepNotConfigured_Is503WithTheAgentsOwnCode()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: false);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        var response = await client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("drafting-not-configured", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        var status = await (await client.GetAsync(Status(id))).ReadJsonAsync();
        Assert.Equal(0, status.GetProperty("completedSteps").GetArrayLength());
    }

    [Fact]
    public async Task RequirementsChangedWhileDesigning_Is409ConflictOnRequirementsDraft()
    {
        var hook = new HookedDesignGenerator();
        using var factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true,
            configureServices: services =>
            {
                services.AddSingleton<IDesignDraftGenerator>(hook);
                services.AddSingleton(new DesignAvailability(true));
            });
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();
        (await client.PostAsync(Advance(id), null)).EnsureSuccessStatusCode();
        (await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();
        hook.OnGenerate = async () =>
        {
            // A newer requirements draft is stored while the design is being generated.
            var store = factory.Services.GetRequiredService<IChangeRequestStore>();
            var drafting = factory.Services.GetRequiredService<IRequirementsDraftingService>();
            store.Update(store.FindById(id)! with { Status = ChangeRequestStatus.Submitted });
            await drafting.DraftAsync(id);
            store.Update(store.FindById(id)! with { Status = ChangeRequestStatus.Approved });
        };

        var response = await client.PostAsync(Advance(id), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("conflict", json.GetProperty("code").GetString());
        Assert.Contains("RequirementsDraft", json.FieldNames());
        var designs = await (await client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();
        Assert.Equal(0, designs.GetArrayLength());
    }

    [Fact]
    public async Task CompletedSteps_CarryRequirementsVersionOnlyForDesigns()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        await host.Client.PostAsync(Advance(id), null);
        await Approve(host, id);
        await host.Client.PostAsync(Advance(id), null);

        var status = await (await host.Client.GetAsync(Status(id))).ReadJsonAsync();

        var steps = status.GetProperty("completedSteps");
        Assert.False(steps[0].TryGetProperty("requirementsVersion", out _));
        Assert.Equal(1, steps[1].GetProperty("requirementsVersion").GetInt32());
    }

    [Fact]
    public async Task AdvanceResponse_DraftHasTheTypedDraftShape()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();

        var body = await (await host.Client.PostAsync(Advance(id), null)).ReadJsonAsync();

        var members = body.GetProperty("draft").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Contains("acceptanceCriteria", members);
        Assert.Contains("functionalRequirements", members);
    }

    [Fact]
    public async Task Status_WritesNothing()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();

        for (var i = 0; i < 3; i++)
            (await host.Client.GetAsync(Status(id))).EnsureSuccessStatusCode();

        Assert.Equal(new[] { "Submitted" }, await host.AuditEventTypesAsync(id));
        Assert.Equal(0, host.Requirements.CallCount);
    }
}
