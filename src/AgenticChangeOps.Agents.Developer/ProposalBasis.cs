namespace AgenticChangeOps.Agents.Developer;

/// <summary>The latest requirements draft, in the Developer's own terms, so no other agent is referenced.</summary>
public sealed record ProposalRequirementsBasis(
    int Version,
    string Summary,
    IReadOnlyList<string> FunctionalRequirements,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions);

/// <summary>A person's recorded verdict on the latest design draft; None while nobody has decided.</summary>
public enum ProposalDesignVerdict
{
    None,
    Accepted,
    Rejected
}

/// <summary>The latest design draft, with the requirements version it was based on and its recorded verdict.</summary>
public sealed record ProposalDesignBasis(
    int Version,
    int RequirementsVersion,
    string ApproachSummary,
    IReadOnlyList<string> AffectedComponents,
    IReadOnlyList<string> ImpactAndRisks,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> RollbackConsiderations,
    IReadOnlyList<string> OpenQuestions,
    ProposalDesignVerdict Decision = ProposalDesignVerdict.None);

/// <summary>What the Developer is given: either part is null when it does not exist.</summary>
public sealed record ProposalBasis(ProposalRequirementsBasis? Requirements, ProposalDesignBasis? Design);

/// <summary>Supplies the latest requirements and design drafts for a change request.</summary>
public interface IProposalBasisSource
{
    ProposalBasis GetLatest(Guid changeRequestId);
}

public enum SamplePathState
{
    OutsideSample,
    Missing,
    File,
    Folder
}

/// <summary>Text of a sample file, or the reason it cannot be used. Never both.</summary>
public sealed record SampleTextResult(string? Text, ProposalRejection? Rejection);

/// <summary>
/// Read-only look at the sample for verification. Implemented outside the Developer over the
/// sample's own path rules, so path safety is not copied here.
/// </summary>
public interface ISampleFiles
{
    SamplePathState Probe(string path);

    SampleTextResult ReadText(string path);
}
