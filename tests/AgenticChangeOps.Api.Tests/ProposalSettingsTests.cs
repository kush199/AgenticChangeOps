using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>contracts/settings-and-failures.md: defaults, validation, and no value ever in a message.</summary>
public sealed class ProposalSettingsTests
{
    private static ProposalSettings Read(Dictionary<string, string?> values) =>
        ProposalSettings.FromConfiguration(name => values.TryGetValue(name, out var v) ? v : null);

    [Fact]
    public void WithNothingSet_TheDefaultsApply()
    {
        var settings = Read(new());

        Assert.Equal(5, settings.Limits.MaxChanges);
        Assert.Equal(6000, settings.Limits.MaxTotalBytes);
        Assert.Equal(ProposalLimits.Defaults, settings.Limits);
    }

    [Fact]
    public void ValidValues_AreUsed()
    {
        var settings = Read(new()
        {
            [ProposalSettings.MaxChangesVariable] = " 3 ",
            [ProposalSettings.MaxBytesVariable] = "1200"
        });

        Assert.Equal(new ProposalLimits(3, 1200), settings.Limits);
    }

    [Theory]
    [InlineData(ProposalSettings.MaxChangesVariable, "0")]
    [InlineData(ProposalSettings.MaxChangesVariable, "-4")]
    [InlineData(ProposalSettings.MaxChangesVariable, "+4")]
    [InlineData(ProposalSettings.MaxChangesVariable, "2.5")]
    [InlineData(ProposalSettings.MaxChangesVariable, "1e3")]
    [InlineData(ProposalSettings.MaxChangesVariable, "many")]
    [InlineData(ProposalSettings.MaxChangesVariable, "")]
    [InlineData(ProposalSettings.MaxChangesVariable, "99999999999999999999")]
    [InlineData(ProposalSettings.MaxBytesVariable, "0")]
    [InlineData(ProposalSettings.MaxBytesVariable, "-1")]
    [InlineData(ProposalSettings.MaxBytesVariable, "6,000")]
    [InlineData(ProposalSettings.MaxBytesVariable, "lots")]
    public void AnInvalidValue_IsReported_ByNameOnly(string name, string value)
    {
        var ex = Assert.Throws<GeneratorConfigurationException>(() => Read(new() { [name] = value }));

        Assert.Contains(name, ex.Message);
        if (value.Length > 1)
            Assert.DoesNotContain(value, ex.Message.Replace(name, string.Empty));
    }

    [Fact]
    public void TheFirstWrongSetting_IsTheOneNamed()
    {
        var ex = Assert.Throws<GeneratorConfigurationException>(() => Read(new()
        {
            [ProposalSettings.MaxChangesVariable] = "0",
            [ProposalSettings.MaxBytesVariable] = "0"
        }));

        Assert.Contains(ProposalSettings.MaxChangesVariable, ex.Message);
    }
}
