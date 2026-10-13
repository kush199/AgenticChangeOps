using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgenticChangeOps.Api.Tests.Baseline;

/// <summary>
/// Replaces values that legitimately differ between runs (generated identifiers and clock
/// timestamps) with stable tokens, numbering identifiers by first appearance. Nothing else in a
/// response is altered.
/// </summary>
public sealed class BaselineNormaliser
{
    private static readonly Regex GuidPattern = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);

    private static readonly Regex TimestampPattern = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?", RegexOptions.Compiled);

    private readonly Dictionary<string, int> _ids = new(StringComparer.OrdinalIgnoreCase);

    public string Normalise(string text)
    {
        var withIds = GuidPattern.Replace(text, match =>
        {
            if (!_ids.TryGetValue(match.Value, out var number))
            {
                number = _ids.Count + 1;
                _ids[match.Value] = number;
            }
            return $"<id-{number}>";
        });

        return TimestampPattern.Replace(withIds, "<timestamp>");
    }
}

/// <summary>Accumulates one text transcript of labelled responses.</summary>
public sealed class TranscriptBuilder
{
    private static readonly string[] IgnoredHeaders = { "Date" };
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly BaselineNormaliser _normaliser = new();
    private readonly StringBuilder _text = new();

    /// <summary>Records the response and returns its body as JSON (undefined when not JSON).</summary>
    public async Task<JsonElement> RecordAsync(string label, HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        _text.Append("### ").AppendLine(label);
        _text.Append("status: ").Append((int)response.StatusCode).Append(' ').AppendLine(response.StatusCode.ToString());

        foreach (var line in HeaderLines(response))
            _text.Append("header: ").AppendLine(_normaliser.Normalise(line));

        _text.Append("body: ").AppendLine(_normaliser.Normalise(FormatBody(body, out var json)));
        _text.AppendLine();
        return json;
    }

    public override string ToString() => _text.ToString().Replace("\r\n", "\n");

    private static IEnumerable<string> HeaderLines(HttpResponseMessage response)
    {
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> all = response.Headers;
        all = all.Concat(response.Content.Headers);

        return all
            .Where(h => !IgnoredHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .Select(h => $"{h.Key}: {string.Join(", ", h.Value)}");
    }

    private static string FormatBody(string body, out JsonElement json)
    {
        json = default;
        if (string.IsNullOrWhiteSpace(body))
            return "<empty>";

        try
        {
            using var document = JsonDocument.Parse(body);
            json = document.RootElement.Clone();
            return "\n" + JsonSerializer.Serialize(json, Indented);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}

/// <summary>Compares text with a golden file under <c>Baseline/golden</c>.</summary>
public static class GoldenFile
{
    public static void Verify(string name, string actual)
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "AgenticChangeOps.Api.Tests", "Baseline", "golden", name);

        if (Environment.GetEnvironmentVariable(ApiBaselineTests.UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path),
            $"Golden file {name} is missing. Capture it on reviewed code with {ApiBaselineTests.UpdateVariable}=1.");
        var expected = File.ReadAllText(path).Replace("\r\n", "\n");

        Assert.Equal(expected, actual);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }
}
