using System.Text.Json;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// The parsed answer object. Reads values by section name with strict type checks; anything
/// missing or of the wrong kind is reported as malformed output.
/// </summary>
public sealed class ModelAnswer
{
    private readonly JsonElement _root;

    internal ModelAnswer(JsonElement root)
    {
        _root = root;
    }

    public string ReadString(string name)
    {
        var element = _root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String)
            throw ModelAnswerReader.Malformed();
        return element.GetString()!;
    }

    public IReadOnlyList<string> ReadList(string name)
    {
        var element = _root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array)
            throw ModelAnswerReader.Malformed();

        var items = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw ModelAnswerReader.Malformed();
            items.Add(item.GetString()!);
        }
        return items;
    }
}
