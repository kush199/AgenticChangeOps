using System.ClientModel;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// Tells whether a failed service response was a content-filter block. The service reports one as
/// a 400 whose body names the filter. The body is only searched for the marker; none of it is kept.
/// </summary>
internal static class ContentFilterProbe
{
    public const string Marker = "content_filter";

    public static bool ResponseMentions(ClientResultException exception)
    {
        try
        {
            var body = exception.GetRawResponse()?.Content?.ToString();
            return body is not null && body.Contains(Marker, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
