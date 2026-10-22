namespace AgenticChangeOps.Agents.Developer;

public enum ProposalStatus
{
    Draft
}

/// <summary>Whether an edit changes an existing sample file or adds a new one.</summary>
public enum ProposalChangeKind
{
    Change,
    Add
}

/// <summary>
/// Why the system refused a proposal the model produced. A fixed reason only: it never carries a
/// path, a quote or any model text.
/// </summary>
public enum ProposalRejection
{
    NoChanges,
    TooManyChanges,
    TooLarge,
    PathOutsideSample,
    FileNotFound,
    QuoteNotFound,
    QuoteAmbiguous,
    NewFileExists,
    OverlappingEdits,
    MalformedEdit
}
