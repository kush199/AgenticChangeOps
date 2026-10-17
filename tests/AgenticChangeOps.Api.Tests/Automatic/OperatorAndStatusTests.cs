using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests.Automatic;

public class OperatorAndStatusTests
{
    private const string Endpoint = "https://synthetic-model.example.invalid/";
    private const string Deployment = "synthetic-deployment-xyz";
    private const string Key = "sk-synthetic-0000";

    private static readonly Dictionary<string, string?> FastOn = new()
    {
        ["ChangeOps:AutoAdvance:Enabled"] = "true",
        ["ChangeOps:AutoAdvance:IntervalSeconds"] = "1"
    };

    private static string Status(Guid id) => $"/change-requests/{id}/workflow";

    private static async Task<T?> PollAsync<T>(Func<Task<T?>> read, Func<T?, bool> done, int seconds = 12)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        T? value = default;
        while (DateTime.UtcNow < deadline)
        {
            value = await read();
            if (done(value)) return value;
            await Task.Delay(100);
        }
        return value;
    }

    [Fact]
    public async Task WithTheFeatureOn_ARequestGetsADraftUnasked_AndTheAuditAndStatusShowIt()
    {
        using var factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: FastOn);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        var status = await PollAsync(
            async () => await (await client.GetAsync(Status(id))).ReadJsonAsync(),
            s => s.GetProperty("completedSteps").GetArrayLength() == 1);

        Assert.Equal(1, status.GetProperty("completedSteps").GetArrayLength());
        Assert.Equal("On", status.GetProperty("automatic").GetProperty("mode").GetString());
        var audit = await (await client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        var entries = audit.EnumerateArray().ToArray();
        var draft = Assert.Single(entries, e => e.GetProperty("eventType").GetString() == "DraftGenerated");
        Assert.True(draft.GetProperty("automatic").GetBoolean());
        var submitted = Assert.Single(entries, e => e.GetProperty("eventType").GetString() == "Submitted");
        Assert.False(submitted.TryGetProperty("automatic", out _));
        var request = await (await client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Submitted", request.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheOperatorEndpoint_ShowsTheLatestCheck_WithExactlyTheDocumentedMembers()
    {
        using var factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: FastOn);
        using var client = factory.CreateClient();

        var json = await PollAsync(
            async () => await (await client.GetAsync("/operator/automatic-advancement")).ReadJsonAsync(),
            j => j.GetProperty("lastCheck").ValueKind == JsonValueKind.Object);

        Assert.True(json.GetProperty("enabled").GetBoolean());
        Assert.Equal(new[] { "enabled", "lastCheck" }, json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        var last = json.GetProperty("lastCheck");
        Assert.Equal(
            new[] { "checkedAt", "examined", "failed", "ran" },
            last.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.True(last.GetProperty("examined").GetInt32() >= 0);
    }

    [Fact]
    public async Task TheWorkflowStatus_KeepsEveryExistingMember_AndGainsOnlyAutomatic()
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        var status = await (await client.GetAsync(Status(id))).ReadJsonAsync();

        var names = status.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "automatic", "changeRequestId", "changeRequestStatus", "completedSteps", "nextStep", "reason", "stage" },
            names);

        var advance = await (await client.PostAsync($"/change-requests/{id}/workflow/advance", null)).ReadJsonAsync();
        Assert.Equal(
            new[] { "draft", "status", "step" },
            advance.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task WithALeakyGenerator_NoResponseOrLogLineCarriesTheMarkers_AndTheCheckLineIsCountsOnly()
    {
        var calls = 0;
        var generator = new LeakyGenerator(request =>
        {
            var n = Interlocked.Increment(ref calls);
            throw n % 2 == 1
                ? new InvalidOperationException($"failed at {Endpoint} using {Deployment} with {Key}")
                : new DraftGenerationException(GenerationFailureCategory.TimedOut);
        });
        using var factory = new ApiFactory(generator, draftingConfigured: true, settings: FastOn);
        using var client = factory.CreateClient();
        var first = await client.SubmitAsync();
        var second = await client.SubmitAsync();

        var json = await PollAsync(
            async () => await (await client.GetAsync("/operator/automatic-advancement")).ReadJsonAsync(),
            j => j.GetProperty("lastCheck").ValueKind == JsonValueKind.Object
                 && j.GetProperty("lastCheck").GetProperty("failed").GetInt32() >= 1);

        var bodies = new List<string>
        {
            json.GetRawText(),
            await (await client.GetAsync(Status(first))).Content.ReadAsStringAsync(),
            await (await client.GetAsync(Status(second))).Content.ReadAsStringAsync(),
            await (await client.GetAsync($"/change-requests/{first}/audit")).Content.ReadAsStringAsync()
        };
        foreach (var text in bodies.Concat(factory.Logs.Lines))
        {
            Assert.DoesNotContain(Endpoint, text);
            Assert.DoesNotContain(Deployment, text);
            Assert.DoesNotContain(Key, text);
        }

        var checkLines = factory.Logs.Lines.Where(l => l.Contains("Automatic advancement check")).ToArray();
        Assert.NotEmpty(checkLines);
        var pattern = new Regex(
            @"Automatic advancement check: examined=\d+ ran=\d+ failed=\d+( categories=[A-Za-z]+:\d+(,[A-Za-z]+:\d+)*)?$");
        Assert.All(checkLines, line => Assert.Matches(pattern, line));
        Assert.DoesNotContain(checkLines, l => l.Contains(first.ToString()) || l.Contains(second.ToString()));
    }

    [Fact]
    public async Task ChecksRepeatEveryInterval_AndNeverRunAtTheSameTime()
    {
        var generator = new OverlapRecordingGenerator(TimeSpan.FromMilliseconds(1300));
        using var factory = new ApiFactory(generator, draftingConfigured: true, settings: FastOn);
        using var client = factory.CreateClient();
        for (var i = 0; i < 2; i++) await client.SubmitAsync();

        await PollAsync(
            () => Task.FromResult<int?>(generator.Completed),
            done => done >= 2, seconds: 15);

        Assert.True(generator.Completed >= 2);
        Assert.Equal(1, generator.Peak);
    }

    private sealed class LeakyGenerator : IRequirementsDraftGenerator
    {
        private readonly Action<ChangeRequest> _behavior;

        public LeakyGenerator(Action<ChangeRequest> behavior) => _behavior = behavior;

        public Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)
        {
            _behavior(changeRequest);
            return Task.FromResult<DraftContent?>(null);
        }
    }

    private sealed class OverlapRecordingGenerator : IRequirementsDraftGenerator
    {
        private readonly TimeSpan _delay;
        private int _active;
        private int _completed;
        private int _peak;

        public OverlapRecordingGenerator(TimeSpan delay) => _delay = delay;

        public int Completed => Volatile.Read(ref _completed);
        public int Peak => Volatile.Read(ref _peak);

        public async Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref _active);
            int seen;
            while ((seen = Volatile.Read(ref _peak)) < now && Interlocked.CompareExchange(ref _peak, now, seen) != seen) { }
            await Task.Delay(_delay, cancellationToken);
            Interlocked.Decrement(ref _active);
            Interlocked.Increment(ref _completed);
            return new DraftContent(
                "Adjust the sample catalogue cache lifetime.",
                new[] { "The catalogue cache lifetime is configurable." },
                new[] { "Changing the lifetime takes effect without a restart." },
                new[] { "The sample catalogue is the only affected system." },
                new[] { "Should the lifetime differ per environment?" });
        }
    }
}
