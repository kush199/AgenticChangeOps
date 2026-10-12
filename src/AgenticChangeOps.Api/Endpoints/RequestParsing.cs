using System.Text.Json;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AgenticChangeOps.Api.Endpoints;

/// <summary>Syntax-only helpers: identifier, body and enum-name parsing. No business rules.</summary>
internal static class RequestParsing
{
    public static bool TryParseId(string value, out Guid id) => Guid.TryParse(value, out id);

    /// <summary>
    /// Reads the body as <typeparamref name="T"/>. Returns null when the body is empty,
    /// malformed, not a JSON object, or the literal null.
    /// </summary>
    public static async Task<T?> ReadBodyAsync<T>(HttpContext context) where T : class
    {
        var options = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            return document.RootElement.Deserialize<T>(options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses an enum by name, ignoring case. Anything else (missing, numeric, unknown)
    /// becomes an out-of-range value, which the existing validators report by field name.
    /// </summary>
    public static TEnum ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        var trimmed = value?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            foreach (var name in Enum.GetNames<TEnum>())
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse<TEnum>(name);
            }
        }

        return (TEnum)Enum.ToObject(typeof(TEnum), -1);
    }

    public static IResult Error((int StatusCode, ErrorResponse Body) error) =>
        new ErrorResult(error.StatusCode, error.Body);
}
