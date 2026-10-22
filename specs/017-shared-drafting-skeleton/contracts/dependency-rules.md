# Contract: Dependency and Neutrality Rules

## Allowed project references

```text
Intake                    -> (nothing)
ModelAccess               -> (nothing)
Drafting                  -> Intake, ModelAccess            (NEW)
Agents.Requirement        -> Intake, ModelAccess, Drafting  (Drafting added)
Agents.Architect          -> Intake, ModelAccess, Drafting  (Drafting added)
Agents.Developer          -> Intake, ModelAccess, Drafting  (Drafting added)
```

No agent references another agent; Drafting references no agent.

| # | Rule | Check |
|---|---|---|
| D1 | Drafting source names no agent (`Requirement`, `Architect`, `Developer`, `Design`, `Proposal`) | `Drafting.Tests` structure scan |
| D2 | Drafting names no agent's own record type, approval type or automatic-advancement type (the change request and the audit entry are Intake-level shapes every agent stores and are allowed, FR-005) | same scan (`RequirementsDraft`, `DesignDraft`, `ChangeProposal`, `ApprovalDecision`, `Automatic`) |
| D3 | Drafting references only Intake and ModelAccess | project-file check |
| D4 | No agent project references another agent | existing agent structure tests |
| D5 | Each agent project references exactly Intake, ModelAccess, Drafting | the three agent structure tests; expected list gains `AgenticChangeOps.Drafting` (FR-011 exception) |
| D6 | The sequence exists once: no agent source contains `The content generator failed (` or a lock on the store around version assignment | `Drafting.Tests` scan over every `src/AgenticChangeOps.Agents.*` folder, so a fourth agent is covered with no edit |
| D7 | A stand-in fourth agent runs against Drafting alone | `Drafting.Tests` stand-in |
| D8 | ModelAccess still has no project reference | existing check, unchanged |
