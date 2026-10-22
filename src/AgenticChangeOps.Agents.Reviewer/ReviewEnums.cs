namespace AgenticChangeOps.Agents.Reviewer;

public enum ReviewStatus
{
    Draft
}

/// <summary>Whether the system re-checked the proposal edits against the sample at all.</summary>
public enum SystemCheckStatus
{
    Performed,
    NotPerformed
}

/// <summary>Whether a proposed edit changes an existing sample file or adds a new one.</summary>
public enum ReviewChangeKind
{
    Change,
    Add
}

/// <summary>
/// What the system's own re-check found for one proposed edit. A fixed outcome only: it never
/// carries a path, a quote or file text.
/// </summary>
public enum EditCheckOutcome
{
    Found,
    QuoteNotFound,
    QuoteAmbiguous,
    FileNotFound,
    PathOutsideSample,
    NewFileFree,
    NewFileExists,
    MalformedEdit
}

public enum FindingCategory
{
    AcceptanceCriteria,
    DesignConsistency,
    UnmentionedRisk
}

/// <summary>Advisory only: nothing in the system reads it.</summary>
public enum FindingSeverity
{
    Low,
    Medium,
    High
}

public enum ReviewDocument
{
    Requirements,
    Design,
    Proposal
}

public enum ReferenceList
{
    FunctionalRequirements,
    AcceptanceCriteria,
    Assumptions,
    OpenQuestions,
    AffectedComponents,
    ImpactAndRisks,
    Dependencies,
    RollbackConsiderations,
    Changes,
    Risks,
    TestIdeas
}

/// <summary>
/// Why the system refused a review the model produced. A fixed reason only: it never carries
/// finding text, a reference or any model text.
/// </summary>
public enum ReviewRejection
{
    TooManyFindings,
    TooLarge,
    MissingReference,
    UnresolvedReference,
    MalformedFinding
}
