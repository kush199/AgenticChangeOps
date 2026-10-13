using System.Text.Json;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Baseline;

/// <summary>
/// Characterization test for slice 009 (shared agent plumbing). Unlike
/// <see cref="ApiBaselineTests"/>, which uses fake generators, this drives the real
/// Requirement and Architect generators (model call, answer reading, section normalising)
/// over HTTP with a scripted model client, and compares status code, headers, normalised body
/// and audit with a golden file captured before the shared code was extracted. It also checks
/// that no endpoint, deployment name or model answer text appears in any response, header or
/// log line. Goldens live in <c>Baseline/golden</c>; set
/// <see cref="ApiBaselineTests.UpdateVariable"/> to 1 to rewrite them (only ever on reviewed,
/// intended change).
/// </summary>
public class SharedPlumbingBaselineTests
{
    private const string SentinelEndpoint = "https://sentinel-endpoint.invalid/";
    private const string SentinelHost = "sentinel-endpoint.invalid";
    private const string SentinelDeployment = "sentinel-deployment";
    private const string AnswerMarker = "sentinel-model-output-7c3a";
    private const int SmallLimit = 400;

    private enum Agent { Requirement, Architect }

    private enum Variant { Default, TinyTimeout, SmallSize }

    private sealed record Case(
        string Label,
        Func<Agent, Func<CancellationToken, Task<ChatModelResponse>>> Behaviour,
        Variant Variant = Variant.Default);

    [Fact]
    public async Task ModelBackedAgents_ReturnTheRecordedResponses_AndLeakNothing()
    {
        var log = new TranscriptBuilder();
        var seen = new List<string>();

        foreach (var agent in Enum.GetValues<Agent>())
            foreach (var variant in Enum.GetValues<Variant>())
                await RunVariantAsync(agent, variant, log, seen);

        GoldenFile.Verify("shared-plumbing-transcript.txt", log.ToString());

        foreach (var text in seen)
        {
            Assert.DoesNotContain(SentinelEndpoint, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SentinelHost, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SentinelDeployment, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(AnswerMarker, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task RunVariantAsync(Agent agent, Variant variant, TranscriptBuilder log, List<string> seen)
    {
        var cases = Cases().Where(c => c.Variant == variant).ToList();
        var options = OptionsFor(variant);
        var model = new ScriptedChatModelClient();
        var basis = new FakeBasisSource();

        using var factory = CreateFactory(agent, options, model, basis);
        using var client = factory.CreateClient();

        foreach (var item in cases)
        {
            model.Behavior = item.Behaviour(agent);
            var id = await client.SubmitAsync();
            var prefix = $"{agent} {item.Label}";

            if (agent == Agent.Architect)
            {
                basis.Set(id, FakeBasisSource.Basis());
                (await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
                    .EnsureSuccessStatusCode();
            }

            var route = agent == Agent.Requirement ? "drafts" : "designs";

            await RecordAsync(log, seen, $"{prefix}: post", await client.PostAsync($"/change-requests/{id}/{route}", null));
            await RecordAsync(log, seen, $"{prefix}: latest", await client.GetAsync($"/change-requests/{id}/{route}/latest"));
            await RecordAsync(log, seen, $"{prefix}: audit", await client.GetAsync($"/change-requests/{id}/audit"));
        }

        seen.AddRange(factory.Logs.Lines);
    }

    private static async Task RecordAsync(
        TranscriptBuilder log, List<string> seen, string label, HttpResponseMessage response)
    {
        seen.Add(await response.Content.ReadAsStringAsync());
        foreach (var header in response.Headers)
            seen.Add($"{header.Key}: {string.Join(",", header.Value)}");
        foreach (var header in response.Content.Headers)
            seen.Add($"{header.Key}: {string.Join(",", header.Value)}");

        await log.RecordAsync(label, response);
    }

    private static ApiFactory CreateFactory(
        Agent agent, AzureOpenAIGeneratorOptions options, ScriptedChatModelClient model, FakeBasisSource basis)
    {
        if (agent == Agent.Requirement)
            return new ApiFactory(new AzureOpenAIDraftGenerator(options, model), draftingConfigured: true);

        return new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(),
            draftingConfigured: true,
            configureServices: services =>
            {
                services.AddSingleton<IDesignDraftGenerator>(new AzureOpenAIDesignGenerator(options, model));
                services.AddSingleton(new DesignAvailability(true));
                services.AddSingleton<IRequirementsBasisSource>(basis);
            });
    }

    private static AzureOpenAIGeneratorOptions OptionsFor(Variant variant) => variant switch
    {
        Variant.TinyTimeout => new(new Uri(SentinelEndpoint), SentinelDeployment, TimeSpan.FromMilliseconds(50), 32000),
        Variant.SmallSize => new(new Uri(SentinelEndpoint), SentinelDeployment, TimeSpan.FromSeconds(30), SmallLimit),
        _ => new(new Uri(SentinelEndpoint), SentinelDeployment, TimeSpan.FromSeconds(30), 32000)
    };

    // ---- the model behaviours (contracts/http-api-unchanged.md rows 1 to 12) ----

    private static IEnumerable<Case> Cases()
    {
        // 1 to 3: valid answers in each accepted shape
        yield return Text("valid bare object", a => Json(Valid(a)));
        yield return Text("valid padded and blank items", a => Json(Mutate(a, d =>
        {
            d[RequiredListKey(a)] = new[] { "  first item  ", "", "   ", "second item" };
            d[TextKey(a)] = "   padded text   ";
        })));
        yield return Text("valid fence unlabelled", a => "```\n" + Json(Valid(a)) + "\n```");
        yield return Text("valid fence json", a => "```json\n" + Json(Valid(a)) + "\n```");
        yield return Text("valid fence JSON upper", a => "  ```JSON\n" + Json(Valid(a)) + "\n```  ");

        // 4: fence problems
        yield return Text("fence other label", a => "```yaml\n" + Json(Tagged(a)) + "\n```");
        yield return Text("fence unterminated", a => "```json\n" + Json(Tagged(a)));
        yield return Text("fence inner fence", a => "```json\n" + Json(Tagged(a)) + "\n```\nmore\n```");
        yield return Text("fence no newline", _ => "```json " + AnswerMarker + " ```");

        // 5: not an object, not JSON
        yield return Text("root array", _ => "[\"" + AnswerMarker + "\"]");
        yield return Text("root string", _ => "\"" + AnswerMarker + "\"");
        yield return Text("not json", _ => "{ not json " + AnswerMarker);
        yield return Text("plain prose", _ => "Here is my answer: " + AnswerMarker);

        // 6: wrong shape
        yield return Text("missing key", a => Json(Mutate(a, d => { d.Remove(RequiredListKey(a)); d["note"] = AnswerMarker; })));
        yield return Text("text value is number", a => Json(Mutate(a, d => { d[TextKey(a)] = 42; d["note"] = AnswerMarker; })));
        yield return Text("text value is null", a => Json(Mutate(a, d => { d[TextKey(a)] = null; d["note"] = AnswerMarker; })));
        yield return Text("list is string", a => Json(Mutate(a, d => { d[RequiredListKey(a)] = AnswerMarker; })));
        yield return Text("list item is number", a => Json(Mutate(a, d => { d[RequiredListKey(a)] = new object[] { 1, AnswerMarker }; })));
        yield return Text("list item is null", a => Json(Mutate(a, d => { d[RequiredListKey(a)] = new object?[] { null, AnswerMarker }; })));

        // 7: content rules after reading
        yield return Text("required text blank", a => Json(Mutate(a, d => { d[TextKey(a)] = "   "; d["note"] = AnswerMarker; })));
        yield return Text("required text empty", a => Json(Mutate(a, d => { d[TextKey(a)] = ""; d["note"] = AnswerMarker; })));
        yield return Text("required list empty", a => Json(Mutate(a, d => { d[RequiredListKey(a)] = Array.Empty<string>(); d["note"] = AnswerMarker; })));
        yield return Text("required list only blanks", a => Json(Mutate(a, d => { d[RequiredListKey(a)] = new[] { " ", "" }; d["note"] = AnswerMarker; })));
        yield return Text("optional list empty is accepted", a => Json(Mutate(a, d => d[OptionalListKey] = Array.Empty<string>())));
        yield return Text("list over item limit", a => Json(Mutate(a, d =>
        {
            d[OptionalListKey] = Enumerable.Range(1, 51).Select(i => $"item {i}").ToArray();
            d["note"] = AnswerMarker;
        })));
        yield return Text("list at item limit", a => Json(Mutate(a, d =>
            d[OptionalListKey] = Enumerable.Range(1, 50).Select(i => $"item {i}").ToArray())));
        yield return Text("item over length", a => Json(Mutate(a, d =>
        {
            d[OptionalListKey] = new[] { new string('x', 2001) };
            d["note"] = AnswerMarker;
        })));
        yield return Text("item at length", a => Json(Mutate(a, d =>
            d[OptionalListKey] = new[] { new string('x', 2000) })));
        yield return Text("text over length", a => Json(Mutate(a, d =>
        {
            d[TextKey(a)] = new string('y', 2001);
            d["note"] = AnswerMarker;
        })));
        yield return Text("text at length", a => Json(Mutate(a, d => d[TextKey(a)] = new string('y', 2000))));

        // 8: size
        yield return new Case("answer cut off", a => ScriptedChatModelClient.ReturnsText(Json(Tagged(a)), wasCutOff: true));
        yield return new Case("answer at size limit", a =>
            ScriptedChatModelClient.ReturnsText(PadTo(Json(Valid(a)), SmallLimit)), Variant.SmallSize);
        yield return new Case("answer over size limit", a =>
            ScriptedChatModelClient.ReturnsText(PadTo(Json(Tagged(a)), SmallLimit + 1)), Variant.SmallSize);

        // 9: empty
        yield return Text("answer empty", _ => string.Empty);
        yield return Text("answer whitespace", _ => "  \n\t ");

        // 10: categorised failures from the client
        foreach (var category in Enum.GetValues<GenerationFailureCategory>())
        {
            var captured = category;
            yield return new Case($"client category {captured}",
                _ => ScriptedChatModelClient.Throws(new DraftGenerationException(captured)));
        }

        // 11: arbitrary failures whose message carries the sentinels
        yield return new Case("client arbitrary exception", _ => ScriptedChatModelClient.Throws(
            new InvalidOperationException($"failed calling {SentinelEndpoint} deployment {SentinelDeployment} {AnswerMarker}")));
        yield return new Case("client timeout exception", _ => ScriptedChatModelClient.Throws(
            new TimeoutException($"slow {SentinelEndpoint} {AnswerMarker}")));
        yield return new Case("client http-like exception", _ => ScriptedChatModelClient.Throws(
            new HttpRequestException($"refused {SentinelHost} {AnswerMarker}")));

        // 12: the model does not answer within the time limit
        yield return new Case("time limit expires", _ => ScriptedChatModelClient.DelaysUntilCancelled(), Variant.TinyTimeout);
    }

    private static Case Text(string label, Func<Agent, string> text) =>
        new(label, a => ScriptedChatModelClient.ReturnsText(text(a)));

    private static string PadTo(string json, int length) =>
        json.Length > length ? throw new InvalidOperationException("Test answer is longer than the target.")
            : json + new string(' ', length - json.Length);

    // ---- synthetic answers; no real names or systems ----

    private static string TextKey(Agent agent) => agent == Agent.Requirement ? "summary" : "approachSummary";

    private static string RequiredListKey(Agent agent) =>
        agent == Agent.Requirement ? "functionalRequirements" : "affectedComponents";

    private const string OptionalListKey = "openQuestions";

    private static Dictionary<string, object?> Valid(Agent agent) => agent == Agent.Requirement
        ? new Dictionary<string, object?>
        {
            ["summary"] = "Move the sample report job to a quieter window.",
            ["functionalRequirements"] = new[] { "The job runs at the new time." },
            ["acceptanceCriteria"] = new[] { "The report arrives inside the window." },
            ["assumptions"] = new[] { "No other batch jobs compete." },
            ["openQuestions"] = new[] { "Do weekends differ?" }
        }
        : new Dictionary<string, object?>
        {
            ["approachSummary"] = "Change the schedule entry of the sample job only.",
            ["affectedComponents"] = new[] { "Sample scheduler configuration" },
            ["impactAndRisks"] = new[] { "Reports arrive later." },
            ["dependencies"] = new[] { "Import finishes first." },
            ["rollbackConsiderations"] = new[] { "Restore the old entry." },
            ["openQuestions"] = new[] { "Who confirms the window?" }
        };

    private static Dictionary<string, object?> Mutate(Agent agent, Action<Dictionary<string, object?>> change)
    {
        var answer = Valid(agent);
        change(answer);
        return answer;
    }

    /// <summary>A valid answer carrying the marker, so a failure that echoes it is caught.</summary>
    private static Dictionary<string, object?> Tagged(Agent agent) =>
        Mutate(agent, d => d["note"] = AnswerMarker);

    private static string Json(Dictionary<string, object?> answer) => JsonSerializer.Serialize(answer);
}
