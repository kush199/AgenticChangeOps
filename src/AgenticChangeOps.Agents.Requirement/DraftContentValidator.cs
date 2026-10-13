using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Requirement;

public sealed class DraftContentValidator
{
    /// <summary>
    /// Trims text, drops blank list items, then checks every section. Returns the normalised
    /// content when there are no errors, otherwise null and one error per failing section.
    /// Which sections are required is this agent's own choice; the rules are shared.
    /// </summary>
    public (DraftContent? Normalised, IReadOnlyList<ValidationError> Errors) Validate(DraftContent? content)
    {
        if (content is null)
            return (null, new[]
            {
                new ValidationError("Generator", "The content generator returned no content.")
            });

        var errors = new List<ValidationError>();

        var summary = Check(SectionNormaliser.Text(content.Summary, "Summary"), "Summary", errors);
        var functional = Check(
            SectionNormaliser.List(content.FunctionalRequirements, "FunctionalRequirements", requireItem: true),
            "FunctionalRequirements", errors);
        var acceptance = Check(
            SectionNormaliser.List(content.AcceptanceCriteria, "AcceptanceCriteria", requireItem: true),
            "AcceptanceCriteria", errors);
        var assumptions = Check(
            SectionNormaliser.List(content.Assumptions, "Assumptions", requireItem: false),
            "Assumptions", errors);
        var questions = Check(
            SectionNormaliser.List(content.OpenQuestions, "OpenQuestions", requireItem: false),
            "OpenQuestions", errors);

        if (errors.Count > 0)
            return (null, errors);

        return (new DraftContent(summary, functional, acceptance, assumptions, questions), errors);
    }

    private static T? Check<T>(SectionOutcome<T> outcome, string section, List<ValidationError> errors)
    {
        if (outcome.Error is not null)
            errors.Add(new ValidationError(section, outcome.Error));

        return outcome.Value;
    }
}
