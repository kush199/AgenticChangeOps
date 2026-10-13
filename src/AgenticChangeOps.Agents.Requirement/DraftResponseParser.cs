using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Requirement;

/// <summary>
/// Turns the model's answer into <see cref="DraftContent"/>. Accepts the JSON object alone or
/// inside a single code fence; present-but-empty values are passed through unchanged. Anything
/// else is reported as malformed output, and the answer text is never put in the exception.
/// Reading is shared (<see cref="ModelAnswerReader"/>); this type only names the sections.
/// </summary>
public static class DraftResponseParser
{
    public static DraftContent Parse(string text) =>
        ModelAnswerReader.Read(text, answer => new DraftContent(
            Summary: answer.ReadString("summary"),
            FunctionalRequirements: answer.ReadList("functionalRequirements"),
            AcceptanceCriteria: answer.ReadList("acceptanceCriteria"),
            Assumptions: answer.ReadList("assumptions"),
            OpenQuestions: answer.ReadList("openQuestions")));
}
