namespace AgenticChangeOps.Agents.Architect.Tests.Fakes;

/// <summary>
/// Records holding list members compare those lists by reference, so tests compare them
/// member by member through these helpers.
/// </summary>
internal static class ContentAssert
{
    public static void Equal(DesignContent expected, DesignContent? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.ApproachSummary, actual!.ApproachSummary);
        Assert.Equal(expected.AffectedComponents, actual.AffectedComponents);
        Assert.Equal(expected.ImpactAndRisks, actual.ImpactAndRisks);
        Assert.Equal(expected.Dependencies, actual.Dependencies);
        Assert.Equal(expected.RollbackConsiderations, actual.RollbackConsiderations);
        Assert.Equal(expected.OpenQuestions, actual.OpenQuestions);
    }

    public static void Equal(RequirementsBasis expected, RequirementsBasis? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Version, actual!.Version);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.Equal(expected.FunctionalRequirements, actual.FunctionalRequirements);
        Assert.Equal(expected.AcceptanceCriteria, actual.AcceptanceCriteria);
        Assert.Equal(expected.Assumptions, actual.Assumptions);
        Assert.Equal(expected.OpenQuestions, actual.OpenQuestions);
    }
}
