using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class SettingsTests
{
    private static (AutomaticAdvancementSettings Settings, List<string> Warnings) Read(
        Dictionary<string, string?> values)
    {
        var warnings = new List<string>();
        var settings = AutomaticAdvancementSettings.FromConfiguration(
            key => values.TryGetValue(key, out var v) ? v : null, warnings.Add);
        return (settings, warnings);
    }

    [Fact]
    public void WithNothingSet_TheDefaultsApply_AndTheFeatureIsOff()
    {
        var (settings, warnings) = Read(new());

        Assert.False(settings.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.Interval);
        Assert.Equal(5, settings.MaxStepsPerCheck);
        Assert.Equal(TimeSpan.FromMinutes(15), settings.FailureWait);
        Assert.Equal(3, settings.MaxConsecutiveFailures);
        Assert.Empty(warnings);
        Assert.Equal(settings, AutomaticAdvancementSettings.Defaults);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("yes", false)]
    [InlineData("1", false)]
    [InlineData("garbage", false)]
    public void TheFeatureIsOnOnlyForAnExplicitBooleanTrue(string value, bool expected)
    {
        var (settings, _) = Read(new() { [AutomaticAdvancementSettings.EnabledKey] = value });

        Assert.Equal(expected, settings.Enabled);
    }

    [Fact]
    public void ValidValues_AreUsed()
    {
        var (settings, warnings) = Read(new()
        {
            [AutomaticAdvancementSettings.EnabledKey] = "true",
            [AutomaticAdvancementSettings.IntervalKey] = "30",
            [AutomaticAdvancementSettings.MaxStepsKey] = "2",
            [AutomaticAdvancementSettings.FailureWaitKey] = "0",
            [AutomaticAdvancementSettings.MaxFailuresKey] = "7"
        });

        Assert.True(settings.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.Interval);
        Assert.Equal(2, settings.MaxStepsPerCheck);
        Assert.Equal(TimeSpan.Zero, settings.FailureWait);
        Assert.Equal(7, settings.MaxConsecutiveFailures);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData(AutomaticAdvancementSettings.IntervalKey, "abc")]
    [InlineData(AutomaticAdvancementSettings.IntervalKey, "0")]
    [InlineData(AutomaticAdvancementSettings.IntervalKey, "-5")]
    [InlineData(AutomaticAdvancementSettings.IntervalKey, "86401")]
    [InlineData(AutomaticAdvancementSettings.MaxStepsKey, "0")]
    [InlineData(AutomaticAdvancementSettings.MaxStepsKey, "1001")]
    [InlineData(AutomaticAdvancementSettings.MaxStepsKey, "x")]
    [InlineData(AutomaticAdvancementSettings.FailureWaitKey, "-1")]
    [InlineData(AutomaticAdvancementSettings.FailureWaitKey, "86401")]
    [InlineData(AutomaticAdvancementSettings.MaxFailuresKey, "0")]
    [InlineData(AutomaticAdvancementSettings.MaxFailuresKey, "101")]
    public void AnInvalidNumber_FallsBackForThatSettingAlone_AndWarnsWithTheKeyOnly(string key, string value)
    {
        var (settings, warnings) = Read(new()
        {
            [AutomaticAdvancementSettings.EnabledKey] = "true",
            [AutomaticAdvancementSettings.MaxFailuresKey] = "9",
            [key] = value
        });

        var d = AutomaticAdvancementSettings.Defaults;
        Assert.True(settings.Enabled);
        if (key != AutomaticAdvancementSettings.MaxFailuresKey)
            Assert.Equal(9, settings.MaxConsecutiveFailures);
        if (key == AutomaticAdvancementSettings.IntervalKey) Assert.Equal(d.Interval, settings.Interval);
        if (key == AutomaticAdvancementSettings.MaxStepsKey) Assert.Equal(d.MaxStepsPerCheck, settings.MaxStepsPerCheck);
        if (key == AutomaticAdvancementSettings.FailureWaitKey) Assert.Equal(d.FailureWait, settings.FailureWait);
        if (key == AutomaticAdvancementSettings.MaxFailuresKey) Assert.Equal(d.MaxConsecutiveFailures, settings.MaxConsecutiveFailures);

        var warning = Assert.Single(warnings);
        Assert.Contains(key, warning);
        Assert.DoesNotContain(value, warning.Replace(key, ""));
        ForbiddenText.AssertAbsent(warning);
    }

    [Fact]
    public void AnInvalidEnabledValue_LeavesTheFeatureOff()
    {
        var (settings, _) = Read(new()
        {
            [AutomaticAdvancementSettings.EnabledKey] = "sometimes",
            [AutomaticAdvancementSettings.IntervalKey] = "10"
        });

        Assert.False(settings.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(10), settings.Interval);
    }
}
