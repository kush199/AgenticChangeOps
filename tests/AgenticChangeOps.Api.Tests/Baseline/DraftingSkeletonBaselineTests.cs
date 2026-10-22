using System.Text;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests.Baseline;

/// <summary>
/// Characterization test for slice 017 (shared drafting skeleton). Records, over HTTP, the
/// Developer agent's proposal operations that no earlier golden covers: unknown and invalid
/// identifiers, requests that are not eligible, missing and outdated drafts, latest and history
/// before and after proposals exist, every rejection reason, every generator failure category
/// and unusable answers, and the tools-off state. The Requirement and Architect operations are
/// recorded by the earlier goldens (<c>transcript.txt</c>, <c>design-operations-transcript.txt</c>,
/// <c>shared-plumbing-transcript.txt</c>, <c>caller-cancellation-transcript.txt</c>), which this
/// slice must leave unchanged. The golden was captured from the code as it stood before the
/// drafting engine was extracted (commit 7bf6f44) and is never rewritten from later code. Rewrite
/// only with <see cref="ApiBaselineTests.UpdateVariable"/> = 1 on reviewed, intended change.
/// Audit entries are recorded as event-type lists only, because tool-call entries carry elapsed
/// times that differ between runs.
/// </summary>
public class DraftingSkeletonBaselineTests
{
    private const string Secret = "SECRET-QUOTE-0099";

    [Fact]
    public async Task DeveloperOperations_ReturnTheRecordedResponses()
    {
        var log = new TranscriptBuilder();
        var notes = new StringBuilder();

        await RecordMainFlowAsync(log, notes);
        await RecordRejectionsAsync(log, notes);
        await RecordGeneratorFailuresAsync(log, notes);
        await RecordToolsOffAsync(log);

        GoldenFile.Verify("drafting-skeleton-transcript.txt", log + "\n" + notes.ToString().Replace("\r\n", "\n"));
    }

    private static async Task RecordMainFlowAsync(TranscriptBuilder log, StringBuilder notes)
    {
        using var host = new ProposalHost();
        var client = host.Client;

        var unknown = Guid.NewGuid();
        await log.RecordAsync("propose unknown id", await host.ProposeAsync(unknown));
        await log.RecordAsync("latest proposal unknown id", await client.GetAsync($"/change-requests/{unknown}/proposals/latest"));
        await log.RecordAsync("proposal history unknown id", await client.GetAsync($"/change-requests/{unknown}/proposals"));
        await log.RecordAsync("propose invalid id", await client.PostAsync("/change-requests/not-a-guid/proposals", null));
        await log.RecordAsync("latest proposal invalid id", await client.GetAsync("/change-requests/not-a-guid/proposals/latest"));
        await log.RecordAsync("proposal history invalid id", await client.GetAsync("/change-requests/not-a-guid/proposals"));

        var submitted = await host.SubmittedAsync();
        host.Basis.Set(submitted, FakeProposalBasis.Current());
        await log.RecordAsync("propose submitted request", await host.ProposeAsync(submitted));

        var noDrafts = await host.ApprovedAsync(withDrafts: false);
        await log.RecordAsync("propose approved no requirements draft", await host.ProposeAsync(noDrafts));

        var noDesign = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(noDesign, new ProposalBasis(FakeProposalBasis.Current().Requirements, null));
        await log.RecordAsync("propose approved no design draft", await host.ProposeAsync(noDesign));

        var outdated = await host.ApprovedAsync(withDrafts: false);
        host.Basis.Set(outdated, new ProposalBasis(FakeProposalBasis.Current(2, 1).Requirements, FakeProposalBasis.Current(1, 1).Design));
        await log.RecordAsync("propose outdated design", await host.ProposeAsync(outdated));
        await log.RecordAsync("latest proposal after refusal", await client.GetAsync($"/change-requests/{outdated}/proposals/latest"));
        await AuditTypesAsync(host, outdated, "audit after refusal", notes);

        var approved = await host.ApprovedAsync();
        await log.RecordAsync("latest proposal before any", await client.GetAsync($"/change-requests/{approved}/proposals/latest"));
        await log.RecordAsync("proposal history before any", await client.GetAsync($"/change-requests/{approved}/proposals"));
        await log.RecordAsync("propose first", await host.ProposeAsync(approved));
        await log.RecordAsync("propose second", await host.ProposeAsync(approved));
        await log.RecordAsync("latest proposal", await client.GetAsync($"/change-requests/{approved}/proposals/latest"));
        await log.RecordAsync("proposal history", await client.GetAsync($"/change-requests/{approved}/proposals"));
        await AuditTypesAsync(host, approved, "audit after proposing", notes);
    }

    private static async Task RecordRejectionsAsync(TranscriptBuilder log, StringBuilder notes)
    {
        foreach (var (label, edits) in Rejections())
        {
            using var host = new ProposalHost(behavior: ProposalHost.ReadsAndProposesExactly(edits));
            var id = await host.ApprovedAsync();
            await log.RecordAsync($"reject {label}", await host.ProposeAsync(id));
            await log.RecordAsync($"history after reject {label}", await host.Client.GetAsync($"/change-requests/{id}/proposals"));
            await AuditTypesAsync(host, id, $"audit after reject {label}", notes);
        }
    }

    private static IEnumerable<(string Label, ProposalHost.Edit[] Edits)> Rejections() => new[]
    {
        ("quote not found", new[] { ProposalHost.ChangeEdit(existing: Secret) }),
        ("quote ambiguous", new[] { ProposalHost.ChangeEdit(existing: "orders.") }),
        ("path outside sample", new[] { ProposalHost.ChangeEdit(path: "../" + Secret) }),
        ("file not found", new[] { ProposalHost.ChangeEdit(path: "orders-service/config/" + Secret) }),
        ("new file exists", new[] { ProposalHost.AddEdit(path: ProposalHost.OrdersSettings) }),
        ("overlapping edits", new[] { ProposalHost.ChangeEdit(), ProposalHost.ChangeEdit() }),
        ("no changes", Array.Empty<ProposalHost.Edit>()),
        ("too many changes", Enumerable.Range(0, 6).Select(i => ProposalHost.AddEdit(path: $"orders-service/notes/n{i}.md")).ToArray()),
        ("too large", new[] { ProposalHost.AddEdit(text: new string('x', 7000)) }),
        ("malformed edit", new[] { new ProposalHost.Edit(ProposalHost.OrdersSettings, "change", null, "x") })
    };

    private static async Task RecordGeneratorFailuresAsync(TranscriptBuilder log, StringBuilder notes)
    {
        foreach (var category in Enum.GetValues<GenerationFailureCategory>())
        {
            using var host = new ProposalHost(behavior: ProposalHost.Throws(new DraftGenerationException(category)));
            var id = await host.ApprovedAsync();
            await log.RecordAsync($"failure {category}", await host.ProposeAsync(id));
            await AuditTypesAsync(host, id, $"audit after failure {category}", notes);
        }

        using (var host = new ProposalHost(behavior: ProposalHost.Throws(new InvalidOperationException("sentinel-exception-text"))))
        {
            var id = await host.ApprovedAsync();
            await log.RecordAsync("failure other exception", await host.ProposeAsync(id));
        }

        foreach (var (label, text) in new[] { ("not json", "not json at all"), ("missing sections", "{ \"summary\": \"s\" }"), ("array", "[]") })
        {
            using var host = new ProposalHost(behavior: ProposalHost.Returns(text));
            var id = await host.ApprovedAsync();
            await log.RecordAsync($"unusable answer {label}", await host.ProposeAsync(id));
        }
    }

    private static async Task RecordToolsOffAsync(TranscriptBuilder log)
    {
        using var host = new ProposalHost(toolsOn: false);
        var id = await host.ApprovedAsync();
        await log.RecordAsync("tools off propose", await host.ProposeAsync(id));
        await log.RecordAsync("tools off latest", await host.Client.GetAsync($"/change-requests/{id}/proposals/latest"));
        await log.RecordAsync("tools off history", await host.Client.GetAsync($"/change-requests/{id}/proposals"));
    }

    private static async Task AuditTypesAsync(ProposalHost host, Guid id, string label, StringBuilder notes)
    {
        var types = (await host.AuditAsync(id)).EnumerateArray()
            .Select(e => e.GetProperty("eventType").GetString());
        notes.Append("### ").Append(label).Append(": ").AppendLine(string.Join(", ", types));
    }
}
