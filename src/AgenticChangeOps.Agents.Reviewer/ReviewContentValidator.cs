using System.Text;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// Shape, count, size and reference rules for the model's findings, in that order
/// (contracts/reference-and-limit-rules.md). Which sections are required is this agent's own
/// choice; the rules for text and lists are shared. A reason is all that ever leaves this type
/// for a rejected review: never finding text or a reference.
/// </summary>
public sealed class ReviewContentValidator
{
    private const string FindingsSection = "Findings";
    private const string OpenQuestionsSection = "OpenQuestions";

    private readonly ReviewLimits _limits;

    public ReviewContentValidator(ReviewLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }

    public ReviewValidation Validate(ReviewContent? content, ReviewBases bases)
    {
        ArgumentNullException.ThrowIfNull(bases);

        if (content is null)
            return Failed(new ValidationError("Generator", "The content generator returned no content."));

        var errors = new List<ValidationError>();

        var questions = Check(
            SectionNormaliser.List(content.OpenQuestions, OpenQuestionsSection, requireItem: false),
            OpenQuestionsSection, errors);
        var findings = ReadFindings(content.Findings, errors);

        // 1. Shape: anything wrong here is an unusable answer, not a refusal.
        if (errors.Count > 0)
            return Failed(errors.ToArray());

        // 2. Count, then 3. size (UTF-8 bytes of statements plus open questions).
        if (findings!.Count > _limits.MaxFindings)
            return Rejected(ReviewRejection.TooManyFindings);

        if (TotalBytes(findings, questions!) > _limits.MaxTotalBytes)
            return Rejected(ReviewRejection.TooLarge);

        // 4. References, finding by finding in order; the first reason found is the one returned.
        var typed = new List<ReviewFinding>(findings.Count);
        foreach (var finding in findings)
        {
            if (finding.References.Count == 0)
                return Rejected(ReviewRejection.MissingReference);

            var references = new List<ReviewReference>(finding.References.Count);
            foreach (var text in finding.References)
            {
                if (ReferenceResolver.Resolve(text, bases) is not { } reference)
                    return Rejected(ReviewRejection.UnresolvedReference);

                references.Add(reference);
            }

            typed.Add(new ReviewFinding(finding.Category, finding.Severity, finding.Statement, references));
        }

        return new ReviewValidation(
            new ValidatedReview(typed, questions!),
            Array.Empty<ValidationError>(),
            null);
    }

    private sealed record ShapedFinding(
        FindingCategory Category, FindingSeverity Severity, string Statement, IReadOnlyList<string> References);

    private static List<ShapedFinding>? ReadFindings(
        IReadOnlyList<ReviewFindingContent>? raw, List<ValidationError> errors)
    {
        if (raw is null)
        {
            errors.Add(new ValidationError(FindingsSection, "Findings section is missing."));
            return null;
        }

        var findings = new List<ShapedFinding>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var number = i + 1;
            var item = raw[i];
            var before = errors.Count;

            var statement = Check(SectionNormaliser.Text(item.Statement, $"Finding {number} statement"), FindingsSection, errors);
            var category = ParseWord<FindingCategory>(item.Category, $"Finding {number} category", errors);
            var severity = ParseWord<FindingSeverity>(item.Severity, $"Finding {number} severity", errors);

            // An absent reference list is "no reference" (a refusal), not a malformed answer.
            var references = Check(
                SectionNormaliser.List(item.References ?? Array.Empty<string>(), $"Finding {number} references", requireItem: false),
                FindingsSection, errors);

            if (errors.Count == before)
                findings.Add(new ShapedFinding(category!.Value, severity!.Value, statement!, references!));
        }

        return findings;
    }

    /// <summary>A word from a fixed set, any case; numbers and anything else are not accepted.</summary>
    private static T? ParseWord<T>(string? word, string label, List<ValidationError> errors) where T : struct, Enum
    {
        var trimmed = word?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            foreach (var name in Enum.GetNames<T>())
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse<T>(name);

        errors.Add(new ValidationError(FindingsSection, $"{label} is missing or not one of the allowed words."));
        return null;
    }

    private static long TotalBytes(IReadOnlyList<ShapedFinding> findings, IReadOnlyList<string> questions) =>
        findings.Sum(f => (long)Encoding.UTF8.GetByteCount(f.Statement))
        + questions.Sum(q => (long)Encoding.UTF8.GetByteCount(q));

    private static ReviewValidation Failed(params ValidationError[] errors) => new(null, errors, null);

    private static ReviewValidation Rejected(ReviewRejection reason) =>
        new(null, Array.Empty<ValidationError>(), reason);

    private static T? Check<T>(SectionOutcome<T> outcome, string section, List<ValidationError> errors)
    {
        if (outcome.Error is not null)
            errors.Add(new ValidationError(section, outcome.Error));

        return outcome.Value;
    }
}
