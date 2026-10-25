namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// The system's own re-check of every proposed edit against the sample (contracts/system-check-rules.md).
/// Reads only. No model is involved, and only a position and an outcome ever leave this type:
/// never a path, a quote or file text.
/// </summary>
public sealed class SampleChecker
{
    private readonly IReviewSampleFiles _files;

    public SampleChecker(IReviewSampleFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
    }

    public SystemCheck Check(IReadOnlyList<ReviewProposedChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        // No sample to look at: the check is recorded as not performed and the review goes on.
        if (!_files.IsAvailable)
            return new SystemCheck(SystemCheckStatus.NotPerformed, Array.Empty<EditCheck>());

        // Every edit is compared with the file as it is, never with another edit's result.
        var edits = new List<EditCheck>(changes.Count);
        for (var i = 0; i < changes.Count; i++)
            edits.Add(new EditCheck(i + 1, Outcome(changes[i])));

        return new SystemCheck(SystemCheckStatus.Performed, edits);
    }

    private EditCheckOutcome Outcome(ReviewProposedChange change) =>
        change.Kind == ReviewChangeKind.Add ? OutcomeForAdd(change) : OutcomeForChange(change);

    private EditCheckOutcome OutcomeForAdd(ReviewProposedChange change)
    {
        // An empty quote on an add is the same as none; an add must not carry one.
        if (!string.IsNullOrEmpty(change.ExistingText))
            return EditCheckOutcome.MalformedEdit;

        return _files.Probe(change.Path) switch
        {
            ReviewSamplePathState.OutsideSample => EditCheckOutcome.PathOutsideSample,
            ReviewSamplePathState.File or ReviewSamplePathState.Folder => EditCheckOutcome.NewFileExists,
            _ => EditCheckOutcome.NewFileFree
        };
    }

    private EditCheckOutcome OutcomeForChange(ReviewProposedChange change)
    {
        if (string.IsNullOrEmpty(change.ExistingText))
            return EditCheckOutcome.MalformedEdit;

        switch (_files.Probe(change.Path))
        {
            case ReviewSamplePathState.OutsideSample:
                return EditCheckOutcome.PathOutsideSample;
            case ReviewSamplePathState.Missing:
            case ReviewSamplePathState.Folder:
                return EditCheckOutcome.FileNotFound;
        }

        var raw = _files.ReadText(change.Path);
        if (raw is null)
            return EditCheckOutcome.FileNotFound;

        // Line endings are the one thing that is not significant: a checkout may use either.
        var text = Normalise(raw);
        var quote = Normalise(change.ExistingText);

        var first = text.IndexOf(quote, StringComparison.Ordinal);
        if (first < 0)
            return EditCheckOutcome.QuoteNotFound;

        return text.IndexOf(quote, first + 1, StringComparison.Ordinal) >= 0
            ? EditCheckOutcome.QuoteAmbiguous
            : EditCheckOutcome.Found;
    }

    private static string Normalise(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
