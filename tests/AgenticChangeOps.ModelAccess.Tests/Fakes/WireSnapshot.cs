using System.Text;
using System.Text.Json;

namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>
/// Turns a recorded request into stable text for comparison and goldens: method and path, header
/// names only (values vary per run), and every JSON body member as a "path = value" line.
/// </summary>
public static class WireSnapshot
{
    public const string UpdateVariable = "AGENTICCHANGEOPS_UPDATE_BASELINE";

    public static string Describe(RecordedRequest request)
    {
        var text = new StringBuilder();
        text.Append(request.Method).Append(' ').AppendLine(Normalise(request.Path));
        foreach (var name in request.Headers.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            text.Append("header: ").AppendLine(name.ToLowerInvariant());
        foreach (var line in BodyLines(request.Body))
            text.AppendLine(line);
        return text.ToString();
    }

    public static IReadOnlyList<string> BodyLines(string body)
    {
        var lines = new List<string>();
        using var document = JsonDocument.Parse(body);
        Walk(document.RootElement, "body", lines);
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static void Walk(JsonElement element, string path, List<string> lines)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Walk(property.Value, path + "." + property.Name, lines);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Walk(item, path + "[" + index++ + "]", lines);
                break;
            default:
                lines.Add(path + " = " + element.GetRawText());
                break;
        }
    }

    // The deployment segment is whatever the test configured; keep the operation shape only.
    private static string Normalise(string path) => path;

    /// <summary>Compares text with a golden file, or rewrites it when the update variable is set.</summary>
    public static void VerifyGolden(string fileName, string actual)
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "AgenticChangeOps.ModelAccess.Tests", "golden", fileName);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual.Replace("\r\n", "\n"));
            return;
        }

        Assert.True(File.Exists(path), $"Golden file {fileName} is missing; capture it on the unchanged code first.");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
    }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }
}
