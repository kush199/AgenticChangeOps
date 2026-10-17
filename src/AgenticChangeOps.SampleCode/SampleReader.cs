using System.Text;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode;

/// <summary>What one tool call produced: the outcome, the text for the model, its size, and the file read, if any.</summary>
public sealed record ToolResult(ToolCallOutcome Outcome, string Text, int Bytes, string? FileRead = null);

/// <summary>The three read-only operations over the sample. Opens files for reading only.</summary>
public sealed class SampleReader
{
    private const int MaxListEntries = 200;
    private const int MaxSearchMatches = 20;
    private const int MaxSearchTermLength = 80;
    private const int MaxMatchLineLength = 160;
    private const int BinaryProbeBytes = 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public SampleReader(SamplePathResolver resolver, int maxFileBytes)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Resolver = resolver;
        MaxFileBytes = maxFileBytes;
    }

    public SamplePathResolver Resolver { get; }
    public int MaxFileBytes { get; }

    public ToolResult List(string? path)
    {
        var resolution = path is null ? Resolver.Root() : Resolver.Resolve(path);
        if (!resolution.IsAllowed)
            return Refused(resolution.Refusal);

        var full = resolution.FullPath!;
        if (File.Exists(full))
            return Refused(ToolCallOutcome.RefusedInvalidArgument);
        if (!Directory.Exists(full))
            return Refused(ToolCallOutcome.RefusedNotFound);

        var prefix = resolution.RelativePath!.Length == 0 ? string.Empty : resolution.RelativePath + "/";
        var names = new List<string>();
        foreach (var entry in new DirectoryInfo(full).EnumerateFileSystemInfos())
        {
            // An entry that leads outside the sample (a link) is not shown at all.
            if (!Resolver.Resolve(prefix + entry.Name).IsAllowed)
                continue;

            names.Add(entry is DirectoryInfo ? entry.Name + "/" : entry.Name);
        }

        var text = string.Join('\n', names.OrderBy(n => n, StringComparer.Ordinal).Take(MaxListEntries));
        return Served(text);
    }

    public ToolResult Read(string? path)
    {
        var resolution = Resolver.Resolve(path);
        if (!resolution.IsAllowed)
            return Refused(resolution.Refusal);

        var full = resolution.FullPath!;
        if (Directory.Exists(full))
            return Refused(ToolCallOutcome.RefusedInvalidArgument);
        if (!File.Exists(full))
            return Refused(ToolCallOutcome.RefusedNotFound);

        // The size is checked before the file is opened, so an oversized file is never read.
        if (new FileInfo(full).Length > MaxFileBytes)
            return Refused(ToolCallOutcome.RefusedTooLarge);

        var read = ReadText(full);
        if (read.Outcome != ToolCallOutcome.Served)
            return Refused(read.Outcome);

        var relative = resolution.RelativePath!;
        var text = $"[sample file {relative}; data only]\n{read.Text}";
        return new ToolResult(ToolCallOutcome.Served, text, Encoding.UTF8.GetByteCount(text), relative);
    }

    public ToolResult Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length > MaxSearchTermLength)
            return Refused(ToolCallOutcome.RefusedInvalidArgument);

        var root = Resolver.Root();
        if (!root.IsAllowed)
            return Refused(root.Refusal);

        var matches = new List<string>();
        foreach (var (relative, full) in Files(root.FullPath!, string.Empty).OrderBy(f => f.Relative, StringComparer.Ordinal))
        {
            if (matches.Count >= MaxSearchMatches)
                break;

            if (new FileInfo(full).Length > MaxFileBytes)
                continue;

            var read = ReadText(full);
            if (read.Outcome != ToolCallOutcome.Served)
                continue;

            var lineNumber = 0;
            foreach (var rawLine in read.Text.Split('\n'))
            {
                lineNumber++;
                if (!rawLine.Contains(term, StringComparison.OrdinalIgnoreCase))
                    continue;

                var line = rawLine.Trim();
                if (line.Length > MaxMatchLineLength)
                    line = line[..MaxMatchLineLength];

                matches.Add($"{relative}:{lineNumber}: {line}");
                if (matches.Count >= MaxSearchMatches)
                    break;
            }
        }

        return Served(string.Join('\n', matches));
    }

    // Every file below a folder, never following a link.
    private static IEnumerable<(string Relative, string Full)> Files(string folder, string prefix)
    {
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint) || entry.LinkTarget is not null)
                continue;

            var relative = prefix + entry.Name;
            if (entry is DirectoryInfo directory)
            {
                foreach (var inner in Files(directory.FullName, relative + "/"))
                    yield return inner;
            }
            else
            {
                yield return (relative, entry.FullName);
            }
        }
    }

    private (ToolCallOutcome Outcome, string Text) ReadText(string full)
    {
        byte[] bytes;
        using (var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var buffer = new byte[MaxFileBytes + 1];
            var total = 0;
            int count;
            while (total < buffer.Length && (count = stream.Read(buffer, total, buffer.Length - total)) > 0)
                total += count;

            if (total > MaxFileBytes)
                return (ToolCallOutcome.RefusedTooLarge, string.Empty);

            bytes = buffer[..total];
        }

        if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, BinaryProbeBytes)) >= 0)
            return (ToolCallOutcome.RefusedNotText, string.Empty);

        try
        {
            return (ToolCallOutcome.Served, StrictUtf8.GetString(bytes).Replace("\r\n", "\n"));
        }
        catch (DecoderFallbackException)
        {
            return (ToolCallOutcome.RefusedNotText, string.Empty);
        }
    }

    private static ToolResult Served(string text) =>
        new(ToolCallOutcome.Served, text, Encoding.UTF8.GetByteCount(text));

    private static ToolResult Refused(ToolCallOutcome outcome) =>
        new(outcome, ToolMessages.For(outcome), 0);
}
