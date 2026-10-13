# Contract: Dependency and edit rules

Carried over from slice 007 (D5, D6) and extended for this slice.

| Rule | Statement | Checked by |
|---|---|---|
| A1 | Every type specific to the Architect is declared once, and only in `src/AgenticChangeOps.Agents.Architect`. | Architect `StructureTests` (declaration scan) |
| A2 | `AgenticChangeOps.Agents.Architect.csproj` references exactly `AgenticChangeOps.Intake` and `AgenticChangeOps.ModelAccess`. | Architect `StructureTests` |
| A3 | Architect source names no other agent namespace (`AgenticChangeOps.Agents.<Other>`). | Architect `StructureTests` |
| A4 | The Requirement agent project, intake services and models, and `ModelAccess` are byte-for-byte unchanged, except the single appended `AuditEventType.DesignGenerated` value. | Review of the diff; `git diff --stat` in the PR description |
| A5 | Intake has no project reference and no knowledge of any agent. | Existing Requirement structure tests plus a check that no design type is declared in intake |
| A6 | Only the API composition root references both agents. | Review; API project references listed in the plan |
| A7 | The Architect's generator never writes a status, an approval or a store record; the service alone stores. | Isolation tests (status and approval unchanged after success and after failure) |
| A8 | No new package reference is added to any project. | Review of `.csproj` diffs |
