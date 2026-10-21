using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.SampleCode;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Lets the Developer's verification look at the sample without the Developer knowing how paths
/// are confined: every path goes through the sample's own path rules, files are read by the
/// sample's own reader (same size limit, text only) and are only ever opened for reading.
/// A refusal never carries the supplied path.
/// </summary>
public sealed class SampleFilesAdapter : ISampleFiles
{
    private readonly SamplePathResolver _resolver;
    private readonly SampleReader _reader;

    public SampleFilesAdapter(string sampleRoot, int maxFileBytes)
    {
        _resolver = new SamplePathResolver(sampleRoot);
        _reader = new SampleReader(_resolver, maxFileBytes);
    }

    public SamplePathState Probe(string path)
    {
        var resolution = _resolver.Resolve(path);
        if (!resolution.IsAllowed)
            return resolution.Refusal == ToolCallOutcome.RefusedNotFound
                ? SamplePathState.Missing
                : SamplePathState.OutsideSample;

        var full = resolution.FullPath!;
        if (Directory.Exists(full))
            return SamplePathState.Folder;

        return File.Exists(full) ? SamplePathState.File : SamplePathState.Missing;
    }

    public SampleTextResult ReadText(string path)
    {
        switch (Probe(path))
        {
            case SamplePathState.OutsideSample:
                return new SampleTextResult(null, ProposalRejection.PathOutsideSample);
            case SamplePathState.Missing:
            case SamplePathState.Folder:
                return new SampleTextResult(null, ProposalRejection.FileNotFound);
        }

        var result = _reader.Read(path);
        if (result.Outcome != ToolCallOutcome.Served)
            return new SampleTextResult(null, ProposalRejection.FileNotFound);

        // The reader puts one "[sample file ...; data only]" line first; the file text follows it.
        var newline = result.Text.IndexOf('\n');
        return new SampleTextResult(newline < 0 ? string.Empty : result.Text[(newline + 1)..], null);
    }
}
