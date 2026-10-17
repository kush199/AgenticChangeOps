using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

public sealed class DesignContentValidator
{
    /// <summary>
    /// Trims text, drops blank list items, then checks every section. Returns the normalised
    /// content when there are no errors, otherwise null and one error per failing section.
    /// Which sections are required is this agent's own choice; the rules are shared.
    /// </summary>
    public (DesignContent? Normalised, IReadOnlyList<ValidationError> Errors) Validate(DesignContent? content)
    {
        if (content is null)
            return (null, new[]
            {
                new ValidationError("Generator", "The content generator returned no content.")
            });

        var errors = new List<ValidationError>();

        var summary = Check(SectionNormaliser.Text(content.ApproachSummary, "ApproachSummary"), "ApproachSummary", errors);
        var components = Check(
            SectionNormaliser.List(content.AffectedComponents, "AffectedComponents", requireItem: true),
            "AffectedComponents", errors);
        var impact = Check(
            SectionNormaliser.List(content.ImpactAndRisks, "ImpactAndRisks", requireItem: true),
            "ImpactAndRisks", errors);
        var dependencies = Check(
            SectionNormaliser.List(content.Dependencies, "Dependencies", requireItem: false),
            "Dependencies", errors);
        var rollback = Check(
            SectionNormaliser.List(content.RollbackConsiderations, "RollbackConsiderations", requireItem: true),
            "RollbackConsiderations", errors);
        var questions = Check(
            SectionNormaliser.List(content.OpenQuestions, "OpenQuestions", requireItem: false),
            "OpenQuestions", errors);

        if (errors.Count > 0)
            return (null, errors);

        // What the tools did is carried through unchanged; only the model's text is normalised.
        return (new DesignContent(
            summary, components, impact, dependencies, rollback, questions,
            content.SampleFilesRead, content.ToolCalls), errors);
    }

    private static T? Check<T>(SectionOutcome<T> outcome, string section, List<ValidationError> errors)
    {
        if (outcome.Error is not null)
            errors.Add(new ValidationError(section, outcome.Error));

        return outcome.Value;
    }
}
