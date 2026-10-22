namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>The latest requirements draft, in the Reviewer's own terms, so no other agent is referenced.</summary>
public sealed record ReviewRequirementsBasis(
    int Version,
    string Summary,
    IReadOnlyList<string> FunctionalRequirements,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions);

/// <summary>The latest design draft, with the requirements version it was based on.</summary>
public sealed record ReviewDesignBasis(
    int Version,
    int RequirementsVersion,
    string ApproachSummary,
    IReadOnlyList<string> AffectedComponents,
    IReadOnlyList<string> ImpactAndRisks,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> RollbackConsiderations,
    IReadOnlyList<string> OpenQuestions);

/// <summary>One edit of the proposal under review. Text only: nothing here is ever applied.</summary>
public sealed record ReviewProposedChange(
    string Path,
    ReviewChangeKind Kind,
    string? ExistingText,
    string ReplacementText);

/// <summary>The latest proposal, with the requirements and design versions it was based on.</summary>
public sealed record ReviewProposalBasis(
    int Version,
    int RequirementsVersion,
    int DesignVersion,
    string Summary,
    IReadOnlyList<ReviewProposedChange> Changes,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> TestIdeas,
    IReadOnlyList<string> OpenQuestions);

/// <summary>What the Reviewer is given: any part is null when it does not exist.</summary>
public sealed record ReviewBases(
    ReviewRequirementsBasis? Requirements,
    ReviewDesignBasis? Design,
    ReviewProposalBasis? Proposal);

/// <summary>Supplies the latest requirements, design and proposal for a change request.</summary>
public interface IReviewBasisSource
{
    ReviewBases GetLatest(Guid changeRequestId);
}
