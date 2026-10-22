using System.Globalization;
using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// Turns a reference written as <c>document.list[position]</c> into a typed one, but only when
/// it names a real entry in the upstream records the review is based on
/// (contracts/reference-and-limit-rules.md). Summaries are single texts, not list entries, so
/// they cannot be referenced.
/// </summary>
public static class ReferenceResolver
{
    private static readonly Regex Form = new(
        @"^([A-Za-z]+)\.([A-Za-z]+)\[([0-9]+)\]\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The typed reference, or null when it does not parse, names an unknown list, or points to nothing.</summary>
    public static ReviewReference? Resolve(string? text, ReviewBases bases)
    {
        ArgumentNullException.ThrowIfNull(bases);
        if (text is null)
            return null;

        var match = Form.Match(text);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var position)
            || position < 1)
            return null;

        var document = match.Groups[1].Value.ToLowerInvariant();
        var list = match.Groups[2].Value.ToLowerInvariant();

        var found = document switch
        {
            "requirements" => ForRequirements(bases.Requirements, list),
            "design" => ForDesign(bases.Design, list),
            "proposal" => ForProposal(bases.Proposal, list),
            _ => null
        };

        if (found is not { } entry || position > entry.Count)
            return null;

        return new ReviewReference(entry.Document, entry.List, position);
    }

    private readonly record struct Entries(ReviewDocument Document, ReferenceList List, int Count);

    private static Entries? ForRequirements(ReviewRequirementsBasis? basis, string list)
    {
        if (basis is null)
            return null;

        return list switch
        {
            "functionalrequirements" => new Entries(ReviewDocument.Requirements, ReferenceList.FunctionalRequirements, basis.FunctionalRequirements.Count),
            "acceptancecriteria" => new Entries(ReviewDocument.Requirements, ReferenceList.AcceptanceCriteria, basis.AcceptanceCriteria.Count),
            "assumptions" => new Entries(ReviewDocument.Requirements, ReferenceList.Assumptions, basis.Assumptions.Count),
            "openquestions" => new Entries(ReviewDocument.Requirements, ReferenceList.OpenQuestions, basis.OpenQuestions.Count),
            _ => null
        };
    }

    private static Entries? ForDesign(ReviewDesignBasis? basis, string list)
    {
        if (basis is null)
            return null;

        return list switch
        {
            "affectedcomponents" => new Entries(ReviewDocument.Design, ReferenceList.AffectedComponents, basis.AffectedComponents.Count),
            "impactandrisks" => new Entries(ReviewDocument.Design, ReferenceList.ImpactAndRisks, basis.ImpactAndRisks.Count),
            "dependencies" => new Entries(ReviewDocument.Design, ReferenceList.Dependencies, basis.Dependencies.Count),
            "rollbackconsiderations" => new Entries(ReviewDocument.Design, ReferenceList.RollbackConsiderations, basis.RollbackConsiderations.Count),
            "openquestions" => new Entries(ReviewDocument.Design, ReferenceList.OpenQuestions, basis.OpenQuestions.Count),
            _ => null
        };
    }

    private static Entries? ForProposal(ReviewProposalBasis? basis, string list)
    {
        if (basis is null)
            return null;

        return list switch
        {
            "changes" => new Entries(ReviewDocument.Proposal, ReferenceList.Changes, basis.Changes.Count),
            "risks" => new Entries(ReviewDocument.Proposal, ReferenceList.Risks, basis.Risks.Count),
            "testideas" => new Entries(ReviewDocument.Proposal, ReferenceList.TestIdeas, basis.TestIdeas.Count),
            "openquestions" => new Entries(ReviewDocument.Proposal, ReferenceList.OpenQuestions, basis.OpenQuestions.Count),
            _ => null
        };
    }
}
