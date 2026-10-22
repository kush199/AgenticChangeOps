using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US4 over HTTP: a proposal the system will not accept, and every model-side failure, stores
/// nothing and returns a reason or a category only (SC-002, SC-006).
/// </summary>
public sealed class ProposalFailureEndpointTests
{
    private const string Secret = "SECRET-QUOTE-0099";

    private static readonly string[] Forbidden =
    {
        Secret, ProposalHost.Endpoint, ProposalHost.Deployment, "sentinel-", "orders.page.size", "queue", "Reserved",
        "orders-service/", "billing-portal/"
    };

    private static async Task AssertNoContentLeakAsync(ProposalHost host, HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        var logs = string.Join("\n", host.Logs.Lines);
        foreach (var marker in Forbidden)
        {
            Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(marker, logs, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(host.SampleFolder, text + logs, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertNothingStoredAsync(ProposalHost host, Guid id)
    {
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/proposals")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
        var audit = (await host.AuditAsync(id)).EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToArray();
        Assert.DoesNotContain("ProposalGenerated", audit);
        Assert.DoesNotContain("ToolCalled", audit);
        Assert.Equal("Approved", await host.StatusAsync(id));
    }

    public static IEnumerable<object[]> Rejections() => new[]
    {
        new object[] { "QuoteNotFound", new[] { ProposalHost.ChangeEdit(existing: Secret) } },
        new object[] { "QuoteNotFound", new[] { ProposalHost.ChangeEdit(), ProposalHost.ChangeEdit(existing: "no such text " + Secret) } },
        new object[] { "QuoteAmbiguous", new[] { ProposalHost.ChangeEdit(existing: "orders.") } },
        new object[] { "PathOutsideSample", new[] { ProposalHost.ChangeEdit(path: "../" + Secret) } },
        new object[] { "PathOutsideSample", new[] { ProposalHost.AddEdit(path: "C:\\" + Secret) } },
        new object[] { "FileNotFound", new[] { ProposalHost.ChangeEdit(path: "orders-service/config/" + Secret) } },
        new object[] { "NewFileExists", new[] { ProposalHost.AddEdit(path: ProposalHost.OrdersSettings) } },
        new object[] { "OverlappingEdits", new[] { ProposalHost.ChangeEdit(), ProposalHost.ChangeEdit() } },
        new object[] { "NoChanges", Array.Empty<ProposalHost.Edit>() },
        new object[] { "TooManyChanges", Enumerable.Range(0, 6).Select(i => ProposalHost.AddEdit(path: $"orders-service/notes/n{i}.md")).ToArray() },
        new object[] { "TooLarge", new[] { ProposalHost.AddEdit(text: new string('x', 7000)) } },
        new object[] { "MalformedEdit", new[] { new ProposalHost.Edit(ProposalHost.OrdersSettings, "change", null, "x") } }
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task ARejectedProposal_Is502ProposalRejected_WithTheReasonOnly_AndStoresNothing(
        string reason, ProposalHost.Edit[] edits)
    {
        using var host = new ProposalHost(behavior: ProposalHost.ReadsAndProposesExactly(edits));
        var id = await host.ApprovedAsync();
        var before = host.SampleFingerprint();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("proposal-rejected", body.GetProperty("code").GetString());
        Assert.Equal(reason, body.GetProperty("category").GetString());
        await AssertNothingStoredAsync(host, id);
        await AssertNoContentLeakAsync(host, response);
        Assert.Equal(before, host.SampleFingerprint());
    }

    public static IEnumerable<object[]> ModelFailures() => new[]
    {
        new object[] { "Unreachable", new DraftGenerationException(GenerationFailureCategory.Unreachable) },
        new object[] { "TimedOut", new DraftGenerationException(GenerationFailureCategory.TimedOut) },
        new object[] { "ResponseTooLarge", new DraftGenerationException(GenerationFailureCategory.ResponseTooLarge) },
        new object[] { "MalformedOutput", new DraftGenerationException(GenerationFailureCategory.MalformedOutput) },
        new object[] { "ToolsUnavailable", new DraftGenerationException(GenerationFailureCategory.ToolsUnavailable) },
        new object[] { "LimitsExhausted", new DraftGenerationException(GenerationFailureCategory.LimitsExhausted) }
    };

    [Theory]
    [MemberData(nameof(ModelFailures))]
    public async Task AModelFailure_Is502GenerationFailed_WithTheCategoryOnly_AndStoresNothing(
        string category, DraftGenerationException failure)
    {
        using var host = new ProposalHost(behavior: ProposalHost.Throws(failure));
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("generation-failed", body.GetProperty("code").GetString());
        Assert.Equal(category, body.GetProperty("category").GetString());
        await AssertNothingStoredAsync(host, id);
        await AssertNoContentLeakAsync(host, response);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"summary\": \"s\" }")]
    [InlineData("[]")]
    public async Task AnUnusableAnswer_Is502_MalformedOutput(string text)
    {
        using var host = new ProposalHost(behavior: ProposalHost.Returns(text));
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MalformedOutput", (await response.ReadJsonAsync()).GetProperty("category").GetString());
        await AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task AnAnswerWithBlankSections_Is502_MalformedOutput_AndNoSectionTextIsForwarded()
    {
        using var host = new ProposalHost(behavior: ProposalHost.Returns(
            "{ \"summary\": \"\", \"changes\": [], \"risks\": [], \"testIdeas\": [], \"openQuestions\": [] }"));
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal("MalformedOutput", (await response.ReadJsonAsync()).GetProperty("category").GetString());
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Summary", text);
        await AssertNothingStoredAsync(host, id);
    }

    [Fact]
    public async Task AnUnexpectedException_Is502_WithNoMessageAttached_AndTheEndpointNeverAppears()
    {
        using var host = new ProposalHost(behavior: ProposalHost.Throws(
            new InvalidOperationException(ProposalHost.Endpoint + " " + Secret)));
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        await AssertNothingStoredAsync(host, id);
        await AssertNoContentLeakAsync(host, response);
    }

    [Fact]
    public async Task ARequestThatHitsTheToolCallLimit_ButStillAnswersValidly_IsStored()
    {
        using var host = new ProposalHost(
            settings: new() { ["AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS"] = "2" },
            behavior: async (session, ct) =>
            {
                for (var i = 0; i < 4; i++)
                    await session.Tools.InvokeAsync("read_file", $"{{\"path\":\"{ProposalHost.OrdersSettings}\"}}", ct);
                return new ChatModelResponse(ProposalHost.Answer(ProposalHost.ChangeEdit()), false);
            });
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var outcomes = (await host.AuditAsync(id)).EnumerateArray()
            .Where(e => e.GetProperty("eventType").GetString() == "ToolCalled")
            .Select(e => e.GetProperty("toolOutcome").GetString()).ToArray();
        Assert.Equal(new[] { "Served", "Served", "RefusedCallLimit", "RefusedCallLimit" }, outcomes);
    }
}
