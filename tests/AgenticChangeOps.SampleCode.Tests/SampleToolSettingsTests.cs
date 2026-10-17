using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>contracts/settings-and-failures.md: off by default, safe defaults, name-only errors.</summary>
public sealed class SampleToolSettingsTests
{
    private static Func<string, string?> Env(params (string Name, string? Value)[] values)
    {
        var map = values.ToDictionary(v => v.Name, v => v.Value);
        return name => map.TryGetValue(name, out var value) ? value : null;
    }

    [Fact]
    public void Nothing_Set_MeansOff_WithTheDocumentedDefaults()
    {
        var settings = SampleToolSettings.FromConfiguration(Env());

        Assert.False(settings.Enabled);
        Assert.Equal(8, settings.MaxToolCalls);
        Assert.Equal(6, settings.MaxModelCalls);
        Assert.Equal(24000, settings.MaxReturnedBytes);
        Assert.Equal(8000, settings.MaxFileBytes);
        Assert.Equal(TimeSpan.FromSeconds(60), settings.TimeLimit);
        Assert.Null(settings.SampleFolder);
    }

    [Fact]
    public void WhenOff_NoOtherSettingIsParsed()
    {
        var settings = SampleToolSettings.FromConfiguration(Env(
            (SampleToolSettings.EnabledVariable, "false"),
            (SampleToolSettings.MaxCallsVariable, "garbage"),
            (SampleToolSettings.MaxModelCallsVariable, "-3"),
            (SampleToolSettings.MaxReturnedBytesVariable, "0"),
            (SampleToolSettings.MaxFileBytesVariable, "1.5"),
            (SampleToolSettings.TimeLimitVariable, "+9"),
            (SampleToolSettings.SampleFolderVariable, "\0")));

        Assert.False(settings.Enabled);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData(" True ")]
    public void TheSwitch_AcceptsTrue_InAnyCase(string value) =>
        Assert.True(SampleToolSettings.FromConfiguration(Env((SampleToolSettings.EnabledVariable, value))).Enabled);

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("")]
    [InlineData("  ")]
    public void TheSwitch_RejectsAnythingElse_AsInvalid_NotAsOff(string value)
    {
        var error = Assert.Throws<GeneratorConfigurationException>(() =>
            SampleToolSettings.FromConfiguration(Env((SampleToolSettings.EnabledVariable, value))));

        Assert.Contains(SampleToolSettings.EnabledVariable, error.Message);
        if (value.Trim().Length > 0)
            Assert.DoesNotContain(value, error.Message);
    }

    [Fact]
    public void WhenOn_ValidValues_AreUsed()
    {
        var settings = SampleToolSettings.FromConfiguration(Env(
            (SampleToolSettings.EnabledVariable, "true"),
            (SampleToolSettings.MaxCallsVariable, "3"),
            (SampleToolSettings.MaxModelCallsVariable, "2"),
            (SampleToolSettings.MaxReturnedBytesVariable, "500"),
            (SampleToolSettings.MaxFileBytesVariable, "100"),
            (SampleToolSettings.TimeLimitVariable, "5"),
            (SampleToolSettings.SampleFolderVariable, "somewhere")));

        Assert.True(settings.Enabled);
        Assert.Equal(3, settings.MaxToolCalls);
        Assert.Equal(2, settings.MaxModelCalls);
        Assert.Equal(500, settings.MaxReturnedBytes);
        Assert.Equal(100, settings.MaxFileBytes);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.TimeLimit);
        Assert.Equal("somewhere", settings.SampleFolder);
    }

    [Theory]
    [InlineData(SampleToolSettings.MaxCallsVariable)]
    [InlineData(SampleToolSettings.MaxModelCallsVariable)]
    [InlineData(SampleToolSettings.MaxReturnedBytesVariable)]
    [InlineData(SampleToolSettings.MaxFileBytesVariable)]
    [InlineData(SampleToolSettings.TimeLimitVariable)]
    public void WhenOn_EachNumericSetting_RejectsZeroNegativeAndNonNumbers_NamingOnlyTheSetting(string setting)
    {
        foreach (var bad in new[] { "0", "-1", "abc", "1.5", "+4", "1e3", "99999999999999999999" })
        {
            var error = Assert.Throws<GeneratorConfigurationException>(() =>
                SampleToolSettings.FromConfiguration(Env(
                    (SampleToolSettings.EnabledVariable, "true"), (setting, bad))));

            Assert.Contains(setting, error.Message);
            Assert.DoesNotContain(bad, error.Message.Replace(setting, string.Empty));
        }
    }
}
