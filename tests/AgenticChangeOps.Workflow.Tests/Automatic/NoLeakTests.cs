using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class NoLeakTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task NothingObservable_CarriesTheLeakyMarkers()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(failureWaitSeconds: 1));
        var id = f.Base.SubmitSynthetic();
        f.Base.RequirementsGenerator.Behavior = (_, _) => throw new InvalidOperationException(ForbiddenText.LeakyMessage);

        var seen = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var summary = await f.Automatic.RunCheckAsync();
            seen.Add(JsonSerializer.Serialize(summary, Json));
            seen.Add(JsonSerializer.Serialize(f.Status(id), Json));
            f.Advance(TimeSpan.FromSeconds(5));
        }

        var row = f.Store.GetAutomaticAttempt(id);
        if (row is not null) seen.Add(JsonSerializer.Serialize(row, Json));
        seen.AddRange(f.Store.GetAuditEntries(id).Select(e => JsonSerializer.Serialize(e, Json)));
        var advance = await f.Workflow.AdvanceAsync(id);
        seen.Add(JsonSerializer.Serialize(advance.Status, Json));

        Assert.All(seen, ForbiddenText.AssertAbsent);
    }

    [Fact]
    public async Task ACategorisedFailure_LeaksNothingEither()
    {
        var f = new AutomaticFixture(AutomaticFixture.On(maxFailures: 1));
        var id = f.Base.SubmitSynthetic();
        f.RequirementsFail(GenerationFailureCategory.MalformedOutput);

        var summary = await f.Automatic.RunCheckAsync();

        ForbiddenText.AssertAbsent(JsonSerializer.Serialize(summary, Json));
        ForbiddenText.AssertAbsent(JsonSerializer.Serialize(f.Status(id), Json));
        ForbiddenText.AssertAbsent(JsonSerializer.Serialize(f.Store.GetAutomaticAttempt(id), Json));
    }

    [Fact]
    public void TheSummaryAndTheStatusBlock_HaveNoFreeTextMembers()
    {
        var allowed = new[]
        {
            typeof(int), typeof(DateTime), typeof(GenerationFailureCategory), typeof(GenerationFailureCategory?),
            typeof(AutomaticMode), typeof(IReadOnlyDictionary<GenerationFailureCategory, int>)
        };

        foreach (var type in new[] { typeof(CheckSummary), typeof(AutomaticAdvancementStatus) })
            foreach (var property in type.GetProperties().Where(p => p.DeclaringType == type))
                Assert.Contains(property.PropertyType, allowed);
    }
}
