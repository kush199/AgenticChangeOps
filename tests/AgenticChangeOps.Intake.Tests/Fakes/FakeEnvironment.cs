namespace AgenticChangeOps.Intake.Tests.Fakes;

/// <summary>Dictionary-backed stand-in for process environment variables.</summary>
public sealed class FakeEnvironment
{
    public const string EndpointVariable = "AGENTICCHANGEOPS_MODEL_ENDPOINT";
    public const string DeploymentVariable = "AGENTICCHANGEOPS_MODEL_DEPLOYMENT";
    public const string TimeoutVariable = "AGENTICCHANGEOPS_MODEL_TIMEOUT_SECONDS";
    public const string MaxCharsVariable = "AGENTICCHANGEOPS_MODEL_MAX_RESPONSE_CHARS";

    private readonly Dictionary<string, string> _values = new();

    public FakeEnvironment Set(string name, string value)
    {
        _values[name] = value;
        return this;
    }

    public FakeEnvironment Remove(string name)
    {
        _values.Remove(name);
        return this;
    }

    public string? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public static FakeEnvironment Valid() => new FakeEnvironment()
        .Set(EndpointVariable, "https://synthetic-models.invalid/")
        .Set(DeploymentVariable, "synthetic-deployment");
}
