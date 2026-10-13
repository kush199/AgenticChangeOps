# Contract: Dependency and Neutrality Rules

These rules are the checkable contract of the slice. Each is enforced by an automated check (named in the right column) so a later agent cannot break them silently.

## Allowed project references

```text
Intake                    -> (nothing)
ModelAccess               -> (nothing; external packages only)
Agents.Requirement        -> Intake, ModelAccess
Api                       -> Intake, ModelAccess, Agents.Requirement
Intake.Tests              -> Intake
ModelAccess.Tests         -> ModelAccess
Agents.Requirement.Tests  -> Agents.Requirement, Intake, ModelAccess
Api.Tests                 -> Api, Intake, ModelAccess, Agents.Requirement
```

A future `Agents.<Other>` takes the same references as `Agents.Requirement` and is referenced only by `Api` (and its own tests). No agent project references another agent project.

## Rules and checks

| # | Rule | Spec | Check |
|---|---|---|---|
| D1 | Intake assembly references no ModelAccess or Agents assembly | FR-004 | `Intake.Tests`: inspect referenced assembly names |
| D2 | No file under `src/AgenticChangeOps.Intake` mentions `RequirementsDraft`, `RequirementAgent` or `AgenticChangeOps.Agents` | FR-004 | `Intake.Tests`: scan `src/AgenticChangeOps.Intake` |
| D3 | ModelAccess assembly references no Intake or Agents assembly | FR-003, FR-005 | `ModelAccess.Tests`: inspect referenced assembly names |
| D4 | ModelAccess source contains no agent name, no prompt text of an agent, no agent configuration key | FR-003 | `ModelAccess.Tests`: scan `src/AgenticChangeOps.ModelAccess` for `Requirement`, `RequirementAgent`, `RequirementsDraft`, `DraftPromptBuilder` |
| D5 | Every Requirement-agent type lives under `src/AgenticChangeOps.Agents.Requirement` | FR-001 | `Agents.Requirement.Tests`: assert the agent's types are in its assembly and `src` has no other file declaring them |
| D6 | No agent project references another agent project | FR-006 | `Agents.Requirement.Tests`: inspect referenced assembly names |
| D7 | Adding an agent edits neither the first agent nor ModelAccess | FR-006 | Documented walk-through in `quickstart.md`, reviewed in PR |

## Neutrality note

The neutral project's settings keep their existing names (`AGENTICCHANGEOPS_MODEL_*`). They name the model, not an agent, so they stay unchanged (FR-011).
