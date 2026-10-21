using System.Text;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>
/// Checks every edit against the real sample files (contracts/verification-rules.md). Reads only.
/// Returns the first reason to reject, or null when the whole proposal is acceptable. A reason is
/// all that ever leaves this type: never a path, a quote or file text.
/// </summary>
public sealed class ProposalVerifier
{
    private readonly ISampleFiles _files;
    private readonly ProposalLimits _limits;

    public ProposalVerifier(ISampleFiles files, ProposalLimits limits)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(limits);
        _files = files;
        _limits = limits;
    }

    public ProposalRejection? Verify(IReadOnlyList<ProposedChange> changes)
    {
        if (changes.Count == 0)
            return ProposalRejection.NoChanges;

        if (changes.Count > _limits.MaxChanges)
            return ProposalRejection.TooManyChanges;

        if (TotalBytes(changes) > _limits.MaxTotalBytes)
            return ProposalRejection.TooLarge;

        // Every quote is checked against the file as it is, never against another edit's result.
        var quotedRanges = new Dictionary<string, List<(int Start, int End)>>(StringComparer.Ordinal);
        var addedPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var change in changes)
        {
            var rejection = change.Kind == ProposalChangeKind.Add
                ? CheckAdd(change, addedPaths)
                : CheckChange(change, quotedRanges);
            if (rejection is not null)
                return rejection;
        }

        return null;
    }

    private ProposalRejection? CheckAdd(ProposedChange change, HashSet<string> addedPaths)
    {
        if (change.ExistingText is not null)
            return ProposalRejection.MalformedEdit;

        switch (_files.Probe(change.Path))
        {
            case SamplePathState.OutsideSample:
                return ProposalRejection.PathOutsideSample;
            case SamplePathState.File:
            case SamplePathState.Folder:
                return ProposalRejection.NewFileExists;
        }

        return addedPaths.Add(change.Path) ? null : ProposalRejection.OverlappingEdits;
    }

    private ProposalRejection? CheckChange(
        ProposedChange change, Dictionary<string, List<(int Start, int End)>> quotedRanges)
    {
        if (string.IsNullOrEmpty(change.ExistingText))
            return ProposalRejection.MalformedEdit;

        var read = _files.ReadText(change.Path);
        if (read.Rejection is { } refused)
            return refused;
        if (read.Text is null)
            return ProposalRejection.FileNotFound;

        // Line endings are the one thing that is not significant: a checkout may use either.
        var text = Normalise(read.Text);
        var quote = Normalise(change.ExistingText);

        var first = text.IndexOf(quote, StringComparison.Ordinal);
        if (first < 0)
            return ProposalRejection.QuoteNotFound;
        if (text.IndexOf(quote, first + 1, StringComparison.Ordinal) >= 0)
            return ProposalRejection.QuoteAmbiguous;

        var range = (Start: first, End: first + quote.Length);
        if (!quotedRanges.TryGetValue(change.Path, out var taken))
            quotedRanges[change.Path] = taken = new List<(int, int)>();
        if (taken.Any(t => range.Start < t.End && t.Start < range.End))
            return ProposalRejection.OverlappingEdits;

        taken.Add(range);
        return null;
    }

    private static string Normalise(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static long TotalBytes(IReadOnlyList<ProposedChange> changes) =>
        changes.Sum(c => (long)(c.ExistingText is null ? 0 : Encoding.UTF8.GetByteCount(c.ExistingText))
                         + Encoding.UTF8.GetByteCount(c.ReplacementText));
}
