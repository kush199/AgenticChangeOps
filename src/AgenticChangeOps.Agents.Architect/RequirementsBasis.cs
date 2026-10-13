namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// What the Architect needs to know about a change request's latest requirements draft. Owned by
/// the Architect so it never has to reference the Requirement agent.
/// </summary>
public sealed record RequirementsBasis(
    int Version,
    string Summary,
    IReadOnlyList<string> FunctionalRequirements,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions);

/// <summary>Supplies the latest requirements draft for a change request, or null when there is none.</summary>
public interface IRequirementsBasisSource
{
    RequirementsBasis? GetLatest(Guid changeRequestId);
}
