namespace AgenticChangeOps.Api.Composition;

/// <summary>Decides where the API listens: loopback only unless the operator says otherwise.</summary>
public static class ListenAddress
{
    public const string UrlsVariable = "ASPNETCORE_URLS";
    public const string DefaultUrl = "http://127.0.0.1:5080";

    public static string Resolve(Func<string, string?> getVariable)
    {
        var configured = getVariable(UrlsVariable)?.Trim();
        return string.IsNullOrEmpty(configured) ? DefaultUrl : configured;
    }
}
