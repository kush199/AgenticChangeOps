using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticChangeOps.Persistence;

internal static class RecordJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
