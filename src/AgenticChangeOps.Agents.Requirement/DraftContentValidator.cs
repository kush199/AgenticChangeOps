using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Requirement;

public sealed class DraftContentValidator
{
    private const int MaxTextLength = 2000;
    private const int MaxListItems = 50;

    /// <summary>
    /// Trims text, drops blank list items, then checks every section. Returns the normalised
    /// content when there are no errors, otherwise null and one error per failing section.
    /// </summary>
    public (DraftContent? Normalised, IReadOnlyList<ValidationError> Errors) Validate(DraftContent? content)
    {
        if (content is null)
            return (null, new[]
            {
                new ValidationError("Generator", "The content generator returned no content.")
            });

        var errors = new List<ValidationError>();

        var summary = content.Summary?.Trim();
        if (string.IsNullOrEmpty(summary))
            errors.Add(new ValidationError("Summary", "Summary is required and must not be blank."));
        else if (summary.Length > MaxTextLength)
            errors.Add(new ValidationError("Summary", $"Summary must not exceed {MaxTextLength} characters."));

        var functional = NormaliseList(content.FunctionalRequirements, "FunctionalRequirements", requireItem: true, errors);
        var acceptance = NormaliseList(content.AcceptanceCriteria, "AcceptanceCriteria", requireItem: true, errors);
        var assumptions = NormaliseList(content.Assumptions, "Assumptions", requireItem: false, errors);
        var questions = NormaliseList(content.OpenQuestions, "OpenQuestions", requireItem: false, errors);

        if (errors.Count > 0)
            return (null, errors);

        return (new DraftContent(summary, functional, acceptance, assumptions, questions), errors);
    }

    private static IReadOnlyList<string>? NormaliseList(
        IReadOnlyList<string>? items,
        string section,
        bool requireItem,
        List<ValidationError> errors)
    {
        if (items is null)
        {
            errors.Add(new ValidationError(section, $"{section} section is missing."));
            return null;
        }

        var kept = items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .ToList();

        if (requireItem && kept.Count == 0)
        {
            errors.Add(new ValidationError(section, $"{section} must contain at least one non-blank item."));
            return null;
        }

        if (kept.Count > MaxListItems)
        {
            errors.Add(new ValidationError(section, $"{section} must not exceed {MaxListItems} items."));
            return null;
        }

        if (kept.Any(i => i.Length > MaxTextLength))
        {
            errors.Add(new ValidationError(section, $"Each {section} item must not exceed {MaxTextLength} characters."));
            return null;
        }

        return kept.AsReadOnly();
    }
}
