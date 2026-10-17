using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode;

/// <summary>The result of checking a path: usable, or refused for a fixed reason.</summary>
public sealed record PathResolution(
    bool IsAllowed, ToolCallOutcome Refusal, string? FullPath, string? RelativePath);

/// <summary>
/// Confines every path to the sample folder: textual rules first (before the disk is touched),
/// then resolution of links segment by segment, then a containment check against the real sample
/// root. A refusal never carries the supplied path.
/// </summary>
public sealed class SamplePathResolver
{
    private const int MaxLength = 260;
    private const int MaxSegments = 16;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public SamplePathResolver(string sampleRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sampleRoot);
        SampleRoot = sampleRoot;
    }

    public string SampleRoot { get; }

    /// <summary>The sample root itself, for a listing with no path.</summary>
    public PathResolution Root()
    {
        var root = RealRoot();
        return root is null
            ? Refuse(ToolCallOutcome.RefusedNotFound)
            : new PathResolution(true, ToolCallOutcome.Served, root, string.Empty);
    }

    public PathResolution Resolve(string? path)
    {
        var textual = CheckText(path, out var segments);
        if (textual is { } refusal)
            return Refuse(refusal);

        var root = RealRoot();
        if (root is null)
            return Refuse(ToolCallOutcome.RefusedNotFound);

        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);

            FileSystemInfo? info = Directory.Exists(current) ? new DirectoryInfo(current)
                : File.Exists(current) ? new FileInfo(current)
                : null;

            if (info is null)
                continue;

            FileSystemInfo? target;
            try
            {
                target = info.ResolveLinkTarget(returnFinalTarget: true);
            }
            catch (IOException)
            {
                return Refuse(ToolCallOutcome.RefusedOutsideSample);
            }

            if (target is not null)
                current = target.FullName;

            if (!IsInside(current, root))
                return Refuse(ToolCallOutcome.RefusedOutsideSample);
        }

        current = Path.GetFullPath(current);
        if (!IsInside(current, root))
            return Refuse(ToolCallOutcome.RefusedOutsideSample);

        return new PathResolution(true, ToolCallOutcome.Served, current, string.Join('/', segments));
    }

    private static PathResolution Refuse(ToolCallOutcome outcome) => new(false, outcome, null, null);

    // Textual rules. Returns the refusal, or null with the plain segments.
    private static ToolCallOutcome? CheckText(string? path, out string[] segments)
    {
        segments = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxLength)
            return ToolCallOutcome.RefusedInvalidArgument;

        foreach (var ch in path)
        {
            if (char.IsControl(ch))
                return ToolCallOutcome.RefusedInvalidArgument;

            // A drive or stream marker, a backslash, or an escape: never part of a sample path.
            if (ch is ':' or '\\' or '%')
                return ToolCallOutcome.RefusedOutsideSample;
        }

        if (path[0] == '/')
            return ToolCallOutcome.RefusedOutsideSample;

        if (path.EndsWith(' ') || path.EndsWith('.'))
            return ToolCallOutcome.RefusedInvalidArgument;

        var parts = path.Split('/');
        if (parts.Length > MaxSegments)
            return ToolCallOutcome.RefusedInvalidArgument;

        foreach (var part in parts)
        {
            if (part.Length == 0)
                return ToolCallOutcome.RefusedInvalidArgument;

            if (part.All(c => c == '.'))
                return ToolCallOutcome.RefusedOutsideSample;

            if (part.EndsWith('.') || part.EndsWith(' ') || part.StartsWith(' '))
                return ToolCallOutcome.RefusedInvalidArgument;
        }

        segments = parts;
        return null;
    }

    // The sample root with any link in it resolved, or null when it is not a folder.
    private string? RealRoot()
    {
        var full = Path.GetFullPath(SampleRoot);
        if (!Directory.Exists(full))
            return null;

        try
        {
            var target = new DirectoryInfo(full).ResolveLinkTarget(returnFinalTarget: true);
            full = target?.FullName ?? full;
        }
        catch (IOException)
        {
            return null;
        }

        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsInside(string candidate, string root)
    {
        var full = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Equals(root, PathComparison))
            return true;

        return full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
    }
}
