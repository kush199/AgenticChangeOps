using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// Turns the model answer into <see cref="ReviewContent"/>. Accepts the JSON object alone or inside
/// a single code fence; anything else is malformed output and the answer text is never put in the
/// exception. Keys other than the findings and the open questions are ignored and never kept.
/// </summary>
public static class ReviewResponseParser
{
    public static ReviewContent Parse(string text) =>
        ModelAnswerReader.Read(text, answer => new ReviewContent(
            Findings: answer
                .ReadObjectListWithStringList("findings", "references", "category", "severity", "statement")
                .Select(item => new ReviewFindingContent(
                    item.Values["category"], item.Values["severity"], item.Values["statement"], item.List))
                .ToArray(),
            OpenQuestions: answer.ReadList("openQuestions")));
}
