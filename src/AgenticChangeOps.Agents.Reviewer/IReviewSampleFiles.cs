namespace AgenticChangeOps.Agents.Reviewer;

public enum ReviewSamplePathState
{
    OutsideSample,
    Missing,
    File,
    Folder
}

/// <summary>
/// Read-only look at the sample for the system's own re-check. Implemented outside the Reviewer
/// over the sample's own path rules, so path safety is not copied here. The model never sees it.
/// </summary>
public interface IReviewSampleFiles
{
    /// <summary>False when the sample cannot be looked at at all (for example, the folder is missing).</summary>
    bool IsAvailable { get; }

    ReviewSamplePathState Probe(string path);

    /// <summary>The text of a file, or null when it cannot be read.</summary>
    string? ReadText(string path);
}
