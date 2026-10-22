namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// One object read from an answer: the requested string members, and one named member read as a
/// list of strings. The list is null when that member was absent or null, and empty when it was
/// present and empty, so a caller can tell the two apart.
/// </summary>
public sealed record ObjectWithList(IReadOnlyDictionary<string, string?> Values, IReadOnlyList<string>? List);
