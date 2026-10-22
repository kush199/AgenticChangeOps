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

    /// <summary>
    /// Reads a list of flat objects: for each item, the requested keys as strings (null when the key
    /// is absent or null). Anything else is malformed output.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string?>> ReadObjectList(string name, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Length == 0)
            throw new ArgumentException("At least one key is required.", nameof(keys));

        var element = _root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array)
            throw ModelAnswerReader.Malformed();

        var items = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw ModelAnswerReader.Malformed();

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (!item.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
                    values[key] = null;
                else if (value.ValueKind == JsonValueKind.String)
                    values[key] = value.GetString();
                else
                    throw ModelAnswerReader.Malformed();
            }

            items.Add(values);
        }

        return items;
    }

    /// <summary>
    /// Reads a list of objects, each with the requested keys as strings (null when absent or null)
    /// and one more member, <paramref name="listKey"/>, as a list of strings. Anything else is
    /// malformed output.
    /// </summary>
    public IReadOnlyList<ObjectWithList> ReadObjectListWithStringList(string name, string listKey, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (string.IsNullOrEmpty(listKey))
            throw new ArgumentException("A list key is required.", nameof(listKey));
        if (keys.Length == 0)
            throw new ArgumentException("At least one key is required.", nameof(keys));

        var element = _root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array)
            throw ModelAnswerReader.Malformed();

        var items = new List<ObjectWithList>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw ModelAnswerReader.Malformed();

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (!item.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
                    values[key] = null;
                else if (value.ValueKind == JsonValueKind.String)
                    values[key] = value.GetString();
                else
                    throw ModelAnswerReader.Malformed();
            }

            // Absent or null is "no list"; present and empty is an empty list. The two stay apart.
            List<string>? list = null;
            if (item.TryGetProperty(listKey, out var listElement) && listElement.ValueKind != JsonValueKind.Null)
            {
                if (listElement.ValueKind != JsonValueKind.Array)
                    throw ModelAnswerReader.Malformed();

                list = new List<string>();
                foreach (var entry in listElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.String)
                        throw ModelAnswerReader.Malformed();
                    list.Add(entry.GetString()!);
                }
            }

            items.Add(new ObjectWithList(values, list));
        }

        return items;
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
