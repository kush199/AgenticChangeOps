using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.SampleCode;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Lets the system's own re-check look at the sample without the Reviewer knowing how paths are
/// confined: every path goes through the sample's own path rules, files are read by the sample's own
/// reader (same size limit, text only) and are only ever opened for reading. Nothing it returns can
/// carry the supplied path.
/// </summary>
public sealed class ReviewSampleFilesAdapter : IReviewSampleFiles
{
    private readonly SamplePathResolver _resolver;
    private readonly SampleReader _reader;

    public ReviewSampleFilesAdapter(string sampleRoot, int maxFileBytes)
    {
        _resolver = new SamplePathResolver(sampleRoot);
        _reader = new SampleReader(_resolver, maxFileBytes);
    }

    /// <summary>True only when the sample folder exists. It is never created.</summary>
    public bool IsAvailable => Directory.Exists(_resolver.SampleRoot);

    public ReviewSamplePathState Probe(string path)
    {
        var resolution = _resolver.Resolve(path);
        if (!resolution.IsAllowed)
            return resolution.Refusal == ToolCallOutcome.RefusedNotFound
                ? ReviewSamplePathState.Missing
                : ReviewSamplePathState.OutsideSample;

        var full = resolution.FullPath!;
        if (Directory.Exists(full))
            return ReviewSamplePathState.Folder;

        return File.Exists(full) ? ReviewSamplePathState.File : ReviewSamplePathState.Missing;
    }

    public string? ReadText(string path)
    {
        if (Probe(path) != ReviewSamplePathState.File)
            return null;

        var result = _reader.Read(path);
        if (result.Outcome != ToolCallOutcome.Served)
            return null;

        // The reader puts one "[sample file ...; data only]" line first; the file text follows it.
        var newline = result.Text.IndexOf('\n');
        return newline < 0 ? string.Empty : result.Text[(newline + 1)..];
    }
}
