using System.Text.Json;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Turns the model's answer into <see cref="DesignContent"/>. Accepts the JSON object alone or
/// inside a single code fence; present-but-empty values are passed through unchanged. Anything
/// else is reported as malformed output, and the answer text is never put in the exception.
/// </summary>
public static class DesignResponseParser
{
    private const string Fence = "```";

    public static DesignContent Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(Unwrap(text));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Malformed();

            return new DesignContent(
                ApproachSummary: ReadString(root, "approachSummary"),
                AffectedComponents: ReadList(root, "affectedComponents"),
                ImpactAndRisks: ReadList(root, "impactAndRisks"),
                Dependencies: ReadList(root, "dependencies"),
                RollbackConsiderations: ReadList(root, "rollbackConsiderations"),
                OpenQuestions: ReadList(root, "openQuestions"));
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (Exception)
        {
            // JsonException, missing key, wrong value kind: all are unusable output. The
            // original exception is dropped so no part of the answer can travel with it.
            throw Malformed();
        }
    }

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

    private static string ReadString(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String)
            throw Malformed();
        return element.GetString()!;
    }

    private static IReadOnlyList<string> ReadList(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array)
            throw Malformed();

        var items = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw Malformed();
            items.Add(item.GetString()!);
        }
        return items;
    }

    private static DraftGenerationException Malformed() =>
        new(GenerationFailureCategory.MalformedOutput);
}
