using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AgenticChangeOps.Api.Tests.Fakes;

public static class HttpHelpers
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(path, body, Web);

    public static Task<HttpResponseMessage> PostRawAsync(this HttpClient client, string path, string raw) =>
        client.PostAsync(path, new StringContent(raw, Encoding.UTF8, "application/json"));

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    public static async Task<Guid> SubmitAsync(this HttpClient client, object? body = null)
    {
        var response = await client.PostJsonAsync("/change-requests", body ?? SyntheticData.ValidSubmission());
        response.EnsureSuccessStatusCode();
        var json = await response.ReadJsonAsync();
        return json.GetProperty("changeRequestId").GetGuid();
    }

    public static string[] FieldNames(this JsonElement error) =>
        error.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString()!)
            .ToArray();
}
