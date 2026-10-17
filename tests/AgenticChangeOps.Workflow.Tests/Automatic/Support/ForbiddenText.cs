namespace AgenticChangeOps.Workflow.Tests.Automatic.Support;

/// <summary>Deliberately leaky markers: nothing observable may ever contain them.</summary>
public static class ForbiddenText
{
    public const string Endpoint = "https://synthetic-model.example.invalid/";
    public const string Deployment = "synthetic-deployment-xyz";
    public const string Key = "sk-synthetic-0000";

    public static readonly string[] All = { Endpoint, Deployment, Key };

    public static string LeakyMessage => $"failed at {Endpoint} using {Deployment} with {Key}";

    public static void AssertAbsent(string text)
    {
        foreach (var marker in All)
            Assert.DoesNotContain(marker, text);
    }
}
