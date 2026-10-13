using System.Text.Json;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// Reads a model answer that is either a bare JSON object or exactly one fenced block, and
/// hands it to the caller's mapping. Anything unusable is reported as malformed output with
/// no part of the answer attached.
/// </summary>
public static class ModelAnswerReader
{
    private const string Fence = "```";

    public static T Read<T>(string text, Func<ModelAnswer, T> map)
    {
        try
        {
            using var document = JsonDocument.Parse(Unwrap(text));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Malformed();

            return map(new ModelAnswer(root));
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (Exception)
        {
            // JsonException, missing key, wrong value kind, or a failing mapping: all are
            // unusable output. The original exception is dropped so no part of the answer can
            // travel with it.
            throw Malformed();
        }
    }

    internal static DraftGenerationException Malformed() =>
        new(GenerationFailureCategory.MalformedOutput);

    private static string Unwrap(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith(Fence, StringComparison.Ordinal))
            return trimmed;

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
            throw Malformed();

        var label = trimmed[Fence.Length..firstLineEnd].Trim();
        if (label.Length > 0 && !label.Equals("json", StringComparison.OrdinalIgnoreCase))
            throw Malformed();

        var remainder = trimmed[(firstLineEnd + 1)..];
        if (!remainder.EndsWith(Fence, StringComparison.Ordinal))
            throw Malformed();

        var inner = remainder[..^Fence.Length];
        if (inner.Contains(Fence, StringComparison.Ordinal))
            throw Malformed();

        return inner.Trim();
    }
}
