using AgenticChangeOps.Agents.Architect;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Stand-in source of the latest requirements draft, keyed by change request.</summary>
public sealed class FakeBasisSource : IRequirementsBasisSource
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RequirementsBasis> _bases = new();

    public void Set(Guid changeRequestId, RequirementsBasis basis)
    {
        lock (_gate)
            _bases[changeRequestId] = basis;
    }

    public RequirementsBasis? GetLatest(Guid changeRequestId)
    {
        lock (_gate)
            return _bases.TryGetValue(changeRequestId, out var basis) ? basis : null;
    }

    public static RequirementsBasis Basis(int version = 1) => new(
        Version: version,
        Summary: "Adjust the sample catalogue cache lifetime.",
        FunctionalRequirements: new[] { "The catalogue cache lifetime is configurable." },
        AcceptanceCriteria: new[] { "Changing the lifetime takes effect without a restart." },
        Assumptions: new[] { "The sample catalogue is the only affected system." },
        OpenQuestions: new[] { "Should the lifetime differ per environment?" });
}
