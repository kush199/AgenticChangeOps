# Contract: Dependency and Neutrality Rules

Rules from slices 007 and 008 stay in force; this slice adds N-rules for the shared pieces. Each is enforced by an automated check so a later agent cannot break it silently.

## Allowed project references (unchanged)

```text
Intake                    -> (nothing)
ModelAccess               -> (nothing; external packages only)
Agents.Requirement        -> Intake, ModelAccess
Agents.Architect          -> Intake, ModelAccess
Api                       -> Intake, ModelAccess, Agents.Requirement, Agents.Architect
```

No agent references another agent. No project reference is added by this slice.

## Rules and checks

| # | Rule | Spec | Check |
|---|---|---|---|
| N1 | Shared source (ModelAccess) names no agent | FR-009 | `ModelAccess.Tests` StructureTests: existing scan plus `Architect`, `DesignDraft`, `DesignContent`, `DesignPromptBuilder` |
| N2 | Shared source names neither intake nor approval nor a stored record | FR-009 | Existing `AgenticChangeOps.Intake` scan plus `ChangeRequest`, `ValidationError`, `ApprovalDecision`, `AuditEvent` |
| N3 | Shared source has no project reference | FR-010 | Existing `ProjectReference` check |
| N4 | Each shared behaviour is declared once, in ModelAccess, and in neither agent project | FR-001, FR-003, FR-004, FR-005, FR-006 | `ModelAccess.Tests` `AgentsHoldNoSharedBehaviourCopyTests`, one scan over every `src/AgenticChangeOps.Agents.*` folder (a further agent is covered with no edit): no agent source contains `CancellationTokenSource`, `JsonDocument.Parse`, the three-backtick fence literal, or blank-item filtering of lists |
| N5 | No agent project references the other | FR-010 | Existing agent StructureTests reference checks |
| N6 | The third-agent stand-in compiles and runs against ModelAccess alone | FR-011 | `StandInAgentTests` |
| N7 | Adding a third agent edits neither ModelAccess nor either agent | FR-011 | `quickstart.md` walk-through, reviewed in the PR |

## Intentional differences that must stay visible

| Area | Requirement agent | Architect agent |
|---|---|---|
| Required list sections | functional requirements, acceptance criteria | affected components, impact and risks, rollback considerations |
| Optional list sections | assumptions, open questions | dependencies, open questions |
| Eligible status | Submitted or Deferred | Approved |
| Extra eligibility input | none | latest requirements draft must exist, and its version must be unchanged at save time |
| Audit event | draft generated | design generated |
| Generator entry point | change request | change request and requirements basis |
| Factory name | `CreateFromEnvironment` | `FromProcessEnvironment` |
