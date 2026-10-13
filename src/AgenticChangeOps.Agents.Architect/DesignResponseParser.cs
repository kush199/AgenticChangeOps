using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Turns the model's answer into <see cref="DesignContent"/>. Accepts the JSON object alone or
/// inside a single code fence; present-but-empty values are passed through unchanged. Anything
/// else is reported as malformed output, and the answer text is never put in the exception.
/// Reading is shared (<see cref="ModelAnswerReader"/>); this type only names the sections.
/// </summary>
public static class DesignResponseParser
{
    public static DesignContent Parse(string text) =>
        ModelAnswerReader.Read(text, answer => new DesignContent(
            ApproachSummary: answer.ReadString("approachSummary"),
            AffectedComponents: answer.ReadList("affectedComponents"),
            ImpactAndRisks: answer.ReadList("impactAndRisks"),
            Dependencies: answer.ReadList("dependencies"),
            RollbackConsiderations: answer.ReadList("rollbackConsiderations"),
            OpenQuestions: answer.ReadList("openQuestions")));
}
