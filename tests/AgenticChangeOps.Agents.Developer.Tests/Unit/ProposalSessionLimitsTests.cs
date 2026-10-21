using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// FR-012: the model-call and time limits belong to the tool session of one proposal request, are
/// handed to the model call with it, and are never carried from one request to the next.
/// </summary>
public sealed class ProposalSessionLimitsTests
{
    private static readonly AzureOpenAIGeneratorOptions Options = new(
        new Uri("https://sentinel-endpoint.invalid/"), "sentinel-deployment", TimeSpan.FromSeconds(30), 32000);

    private static readonly ChangeRequest Request = new(
        Guid.NewGuid(), "Move nightly report schedule", "Move the job.", "Avery Quill",
        "Sample Billing Portal", Priority.Medium, ChangeType.ConfigChange,
        ChangeRequestStatus.Approved, DateTime.UnixEpoch);

    [Fact]
    public async Task TheModelCall_ReceivesTheSessionsOwnLimits_ForEachRequest()
    {
        var factory = new FakeToolSessionFactory();
        var limits = new Queue<(int Calls, TimeSpan Time)>(new[] { (3, TimeSpan.FromSeconds(7)), (9, TimeSpan.FromSeconds(21)) });
        factory.Create = () =>
        {
            var next = limits.Dequeue();
            return new FakeToolSession(next.Calls, next.Time);
        };
        var seen = new List<(int Calls, TimeSpan Time)>();
        var client = new FakeToolChatModelClient
        {
            Behavior = (session, _) =>
            {
                seen.Add((session.MaxModelCalls, session.TimeLimit));
                return Task.FromResult(new ChatModelResponse(FakeProposalGenerator.ValidAnswerJson(), false));
            }
        };
        var generator = new AzureOpenAIProposalGenerator(Options, new FakeChatModelClient(), factory, client);

        await generator.GenerateAsync(Request, FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(), CancellationToken.None);
        await generator.GenerateAsync(Request, FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(), CancellationToken.None);

        Assert.Equal(new[] { (3, TimeSpan.FromSeconds(7)), (9, TimeSpan.FromSeconds(21)) }, seen);
        Assert.Equal(2, factory.Opened.Count);
        Assert.NotSame(factory.Opened[0], factory.Opened[1]);
    }

    [Fact]
    public async Task CallsMadeInOneRequest_AreNotCountedInTheNext()
    {
        var factory = new FakeToolSessionFactory();
        var client = new FakeToolChatModelClient
        {
            Behavior = (session, _) =>
            {
                session.Read("billing-portal/config/portal.settings");
                return Task.FromResult(new ChatModelResponse(FakeProposalGenerator.ValidAnswerJson(), false));
            }
        };
        var generator = new AzureOpenAIProposalGenerator(Options, new FakeChatModelClient(), factory, client);

        var first = await generator.GenerateAsync(Request, FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(), CancellationToken.None);
        var second = await generator.GenerateAsync(Request, FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(), CancellationToken.None);

        Assert.Single(first!.ToolCalls!);
        Assert.Single(second!.ToolCalls!);
        Assert.Single(factory.Opened[0].Calls);
        Assert.Single(factory.Opened[1].Calls);
    }
}
