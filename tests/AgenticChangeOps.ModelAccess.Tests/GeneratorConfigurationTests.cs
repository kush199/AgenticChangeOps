using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.ModelAccess.Tests;

public class GeneratorConfigurationTests
{
    private static AzureOpenAIGeneratorOptions Load(FakeEnvironment env) =>
        AzureOpenAIGeneratorOptions.FromEnvironment(env.Get);

    private static GeneratorConfigurationException Rejects(FakeEnvironment env) =>
        Assert.Throws<GeneratorConfigurationException>(() => Load(env));

    // --- required settings ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingBlankOrWhitespaceEndpoint_NamesTheEndpointSetting(string? value)
    {
        var env = FakeEnvironment.Valid();
        if (value is null) env.Remove(FakeEnvironment.EndpointVariable);
        else env.Set(FakeEnvironment.EndpointVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.EndpointVariable, ex.SettingName);
        Assert.Contains(FakeEnvironment.EndpointVariable, ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingBlankOrWhitespaceDeployment_NamesTheDeploymentSetting(string? value)
    {
        var env = FakeEnvironment.Valid();
        if (value is null) env.Remove(FakeEnvironment.DeploymentVariable);
        else env.Set(FakeEnvironment.DeploymentVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.DeploymentVariable, ex.SettingName);
    }

    // --- placeholders --------------------------------------------------------------------

    [Theory]
    [InlineData("<your-endpoint>")]
    [InlineData("{endpoint}")]
    [InlineData("YOUR_ENDPOINT")]
    [InlineData("replace-with-endpoint")]
    [InlineData("https://placeholder.invalid/")]
    public void PlaceholderEndpoint_IsRejected(string value)
    {
        var env = FakeEnvironment.Valid().Set(FakeEnvironment.EndpointVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.EndpointVariable, ex.SettingName);
    }

    [Theory]
    [InlineData("<your-deployment>")]
    [InlineData("{deployment}")]
    [InlineData("YOUR_DEPLOYMENT")]
    [InlineData("replace-me")]
    [InlineData("my-Placeholder-name")]
    public void PlaceholderDeployment_IsRejected(string value)
    {
        var env = FakeEnvironment.Valid().Set(FakeEnvironment.DeploymentVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.DeploymentVariable, ex.SettingName);
    }

    // --- endpoint shape ------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("/relative/path")]
    [InlineData("http://sentinel-host.invalid/")]
    [InlineData("ftp://sentinel-host.invalid/")]
    public void NonAbsoluteOrNonHttpsEndpoint_IsRejected(string value)
    {
        var env = FakeEnvironment.Valid().Set(FakeEnvironment.EndpointVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.EndpointVariable, ex.SettingName);
    }

    // --- limits --------------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    public void InvalidTimeout_NamesTheTimeoutSetting(string value)
    {
        var env = FakeEnvironment.Valid().Set(FakeEnvironment.TimeoutVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.TimeoutVariable, ex.SettingName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    public void InvalidMaxResponseCharacters_NamesTheSizeSetting(string value)
    {
        var env = FakeEnvironment.Valid().Set(FakeEnvironment.MaxCharsVariable, value);

        var ex = Rejects(env);

        Assert.Equal(FakeEnvironment.MaxCharsVariable, ex.SettingName);
    }

    // --- valid ---------------------------------------------------------------------------

    [Fact]
    public void OnlyTheTwoRequiredSettings_UsesDocumentedDefaults()
    {
        var options = Load(FakeEnvironment.Valid());

        Assert.Equal(new Uri("https://synthetic-models.invalid/"), options.Endpoint);
        Assert.Equal("synthetic-deployment", options.DeploymentName);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RequestTimeout);
        Assert.Equal(32000, options.MaxResponseCharacters);
    }

    [Fact]
    public void ExplicitLimits_AreUsed()
    {
        var env = FakeEnvironment.Valid()
            .Set(FakeEnvironment.TimeoutVariable, "5")
            .Set(FakeEnvironment.MaxCharsVariable, "1000");

        var options = Load(env);

        Assert.Equal(TimeSpan.FromSeconds(5), options.RequestTimeout);
        Assert.Equal(1000, options.MaxResponseCharacters);
    }

    [Fact]
    public void SurroundingWhitespaceInValues_IsTrimmed()
    {
        var env = FakeEnvironment.Valid()
            .Set(FakeEnvironment.DeploymentVariable, "  synthetic-deployment  ")
            .Set(FakeEnvironment.TimeoutVariable, " 10 ");

        var options = Load(env);

        Assert.Equal("synthetic-deployment", options.DeploymentName);
        Assert.Equal(TimeSpan.FromSeconds(10), options.RequestTimeout);
    }

    // --- no key, no echo -----------------------------------------------------------------

    [Fact]
    public void NoKeyVariableOfAnyKind_IsNeededOrRead()
    {
        var read = new List<string>();
        var env = FakeEnvironment.Valid();

        AzureOpenAIGeneratorOptions.FromEnvironment(name =>
        {
            read.Add(name);
            return env.Get(name);
        });

        Assert.All(read, name =>
        {
            Assert.StartsWith("AGENTICCHANGEOPS_MODEL_", name);
            foreach (var word in new[] { "KEY", "TOKEN", "SECRET", "PASSWORD" })
                Assert.DoesNotContain(word, name, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData(FakeEnvironment.EndpointVariable, "http://sentinel-host.invalid/")]
    [InlineData(FakeEnvironment.EndpointVariable, "YOUR_SENTINEL_VALUE")]
    [InlineData(FakeEnvironment.DeploymentVariable, "YOUR_SENTINEL_VALUE")]
    [InlineData(FakeEnvironment.TimeoutVariable, "sentinel-value")]
    [InlineData(FakeEnvironment.MaxCharsVariable, "-77-sentinel")]
    public void RejectionMessages_NeverContainTheSuppliedValue(string variable, string value)
    {
        var env = FakeEnvironment.Valid().Set(variable, value);

        var ex = Rejects(env);

        Assert.DoesNotContain(value, ex.Message);
        Assert.Contains(variable, ex.Message);
    }

}
