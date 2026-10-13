namespace AgenticChangeOps.ModelAccess;

/// <summary>The normalised value, or the error message for the section; exactly one is set.</summary>
public sealed record SectionOutcome<T>(T? Value, string? Error);

/// <summary>
/// Trimming, blank-item removal and size limits for text and list sections. Each agent says
/// which of its sections require an item and turns a returned error into its own result.
/// </summary>
public static class SectionNormaliser
{
    public const int MaxTextLength = 2000;
    public const int MaxListItems = 50;

    public static SectionOutcome<string> Text(string? value, string section)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return new SectionOutcome<string>(null, $"{section} is required and must not be blank.");

        if (trimmed.Length > MaxTextLength)
            return new SectionOutcome<string>(null, $"{section} must not exceed {MaxTextLength} characters.");

        return new SectionOutcome<string>(trimmed, null);
    }

    public static SectionOutcome<IReadOnlyList<string>> List(
        IReadOnlyList<string>? items, string section, bool requireItem)
    {
        if (items is null)
            return Fail($"{section} section is missing.");

        var kept = items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .ToList();

        if (requireItem && kept.Count == 0)
            return Fail($"{section} must contain at least one non-blank item.");

        if (kept.Count > MaxListItems)
            return Fail($"{section} must not exceed {MaxListItems} items.");

        if (kept.Any(i => i.Length > MaxTextLength))
            return Fail($"Each {section} item must not exceed {MaxTextLength} characters.");

        return new SectionOutcome<IReadOnlyList<string>>(kept.AsReadOnly(), null);
    }

    private static SectionOutcome<IReadOnlyList<string>> Fail(string error) =>
        new(null, error);
}
