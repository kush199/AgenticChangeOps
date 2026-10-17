# Implementation Plan: Fix Three Rough Edges From Manual Testing

**Branch**: `fix/rough-edges-from-manual-testing` | **Date**: 2026-10-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-fix-rough-edges/spec.md`

## Summary

Three small corrections, each in code that already exists. No new type, project, operation, setting,
package or stored field.

1. **Validation messages.** `ChangeRequestValidator.ValidateEnum` and
   `ApprovalDecisionValidator.ValidateDecision` build their message from the rejected value, so the
   caller sees `Priority '-1' is not valid` (the API turns every unrecognised text into the
   out-of-range value `-1`). Both messages stop using the value: they name the field and list the
   accepted values only, in one wording for all three fields.
2. **Approved request without a requirements draft.** Two deterministic rules change, so the dead
   end becomes an ordinary due step: `RequirementsDraftingService` treats an Approved request with
   no draft as draftable, and `WorkflowRules.Derive` returns `RequirementsDraft` as the next step
   for it instead of a stop. Manual advance, automatic advancement and the status read all go
   through those two rules already, so they need no change. `ApprovalDecisionValidator`'s status
   rules are not touched.
3. **Check order on the two drafting operations.** `DraftEndpoints.DraftAsync` and
   `DesignEndpoints.DesignAsync` look the request up (through `IChangeRequestService.GetById`, as
   `DecisionEndpoints.GetClearance` already does) between the identifier check and the
   not-configured check. An unknown identifier returns `ErrorMapper.NotFound()`, the same body the
   services' own not-found produces through `ErrorMapper.ForDrafting` / `ForDesigning`.

The exact before/after of every changed response is in
[contracts/response-changes.md](contracts/response-changes.md). Decisions and rejected alternatives
are in [research.md](research.md).

## Technical Context

**Language/Version**: C# on .NET 8 (`net8.0`, as every existing project)

**Primary Dependencies**: none new

**Storage**: unchanged. No layout change; the draft and its `DraftGenerated` audit entry have the
same shape (the entry's `StatusBefore`/`StatusAfter` are simply `Approved`, an existing value).

**Testing**: xUnit with the existing fakes; no live model call. Shared store contract suite
unchanged.

**Target Platform**: Local Windows/Linux process (unchanged)

**Project Type**: Existing multi-project web-service solution

**Performance Goals**: none; one extra in-process store read on two operations.

**Constraints**: every response not listed in the contract is byte-identical in status, body,
headers and error code. No error, log or response may contain caller text, model text, endpoint,
deployment name or credential.

**Scale/Scope**: 5 source files edited, no file added to `src/`.

## Constitution Check

*Constitution v1.0.4.*

| Principle | Result |
|---|---|
| I. Synthetic data only | Pass. Tests use the existing synthetic fixtures. The hostile-text and long-text cases use generated filler, not real content. |
| II. Deterministic routing | Pass. Both rule changes are plain C# predicates (`IsDraftable`, `WorkflowRules.Derive`). No model decides what is due or whether a transition happens. |
| III. Human approval gates | Pass. Approval and the decision rules are untouched. A requirements draft is already non-consequential content produced before approval; producing it after approval does not commit, trigger or advance anything past a checkpoint. Automatic advancement of this step is covered by the 013 classification (drafting steps only) and is not widened. |
| IV. Typed auditable records | Pass. No record type or field changes. The draft and audit entry keep their shape; the audit entry still carries the automatic marker when produced automatically. |
| V. Spec-driven | Pass. Spec exists; this plan and `tasks.md` need explicit human approval before any code. |
| VI. Test-first | Pass, to be followed. Failing tests are written and approved before the three rules change. No live model call in the suite. |
| VII. Simplicity | Pass. No new agent, abstraction or helper. The not-found check is two lines in each of two endpoints and reuses an existing service method. |

No violations; Complexity Tracking is empty. **Post-design re-check:** unchanged, no violation.

## Project Structure

### Documentation (this feature)

```text
specs/014-fix-rough-edges/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── response-changes.md
├── checklists/requirements.md
└── tasks.md              # /speckit-tasks, not created here
```

### Source Code (files edited)

```text
src/
├── AgenticChangeOps.Intake/Services/
│   ├── ChangeRequestValidator.cs          # ValidateEnum message (priority, change type)
│   └── ApprovalDecisionValidator.cs       # ValidateDecision message
├── AgenticChangeOps.Agents.Requirement/
│   └── RequirementsDraftingService.cs     # eligibility: Approved with no draft is draftable
├── AgenticChangeOps.Workflow/
│   └── WorkflowRules.cs                   # Approved, no requirements -> next step RequirementsDraft
└── AgenticChangeOps.Api/Endpoints/
    ├── DraftEndpoints.cs                  # unknown id -> not found before not-configured
    └── DesignEndpoints.cs                 # same

tests/                                      # see "Tests that assert a changed response"
```

**Structure Decision**: edit in place. `WorkflowReasonCode.RequirementsDraftRequired` is no longer
produced by the rules but is left in the enum: removing a public member is not needed to fix the
edge and would be a change nobody asked for. `tasks.md` should note it as unused rather than delete
it.

## Tests that assert a changed response

Determined now (spec Assumption deferred this to planning). Only these existing tests may change an
assertion (FR-011); every other test and golden line must stay as it is.

| Change | Existing test or baseline | Why it must change |
|---|---|---|
| 1 messages | `tests/AgenticChangeOps.Api.Tests/Baseline/golden/transcript.txt` lines ~53, 57, 72, 398 | Show `'-1'`. |
| 2 Approved, no draft | `Baseline/golden/transcript.txt` "draft after approval" (~line 444) | Now accepted (201) instead of 409. The steps after it in the transcript follow from the new draft and are re-recorded. |
| 2 | `DraftEndpointTests.Draft_OnApprovedRequest_Returns409WithSpecificReasonAndGeneratorNotCalled` | Asserts the 409 for an Approved request with no draft. |
| 2 | `DraftEligibilityTests`: the `Approved` rows of `Draft_NonDraftableStatus_ReturnsStatusErrorNamingTheStatus` and `Draft_RefusedRequest_NeverCallsGeneratorAndStoresNothing`; `Draft_RequestBecomesApprovedDuringGeneration_IsRefusedAndConsumesNoVersion` | Each asserts refusal of an Approved request with no draft. The `Rejected` and `UnderReview` rows stay. |
| 2 | `DraftingFailureCategoryTests.EligibilityFailure_CategoryIsNull` | Uses Approved with no draft as its refusal example; switch it to a case that is still refused. |
| 2 | `WorkflowRulesTests.Row7_ApprovedWithoutRequirements_NeedsRequirementsDraft`; `AdvanceRefusalTests.ApprovedWithoutRequirementsDraft_NeedsOne` | Assert the stop with `RequirementsDraftRequired`. |
| 3 order | none found | `NotConfiguredTests` and the design baseline use a known identifier; `design-operations-transcript.txt` is unchanged. A new test covers the unknown identifier. |

New tests (written first, shown for approval): the three messages (exact list, no `-1`, no echo, no
long or hostile text in the response or the one log line); Approved + no draft accepted, status
shows the due step, manual advance and an automatic check complete it; Approved + draft still
refused with today's body; Submitted/Deferred/Rejected/UnderReview unchanged; approval with no
draft still accepted; both operations with drafting unconfigured for malformed, unknown and known
identifiers; the unknown-identifier response equal to the configured one.

## Complexity Tracking

No violations. No additive record change (Principle IV needs nothing recorded).

## Constitution compliance check (pre-code), v1.0.4, 2026-10-17

| Principle | Result |
|---|---|
| I. Synthetic data only | Pass. Existing synthetic fixtures; hostile and long text in tests is generated filler with made-up markers. |
| II. Deterministic routing | Pass. `IsDraftable` and `WorkflowRules.Derive` are plain C# predicates over recorded facts; no model decides what is due or whether a transition happens. |
| III. Human approval gates | Pass. Approval and the human gate are untouched. A requirements draft is content, already produced before approval; it is non-consequential and commits, triggers and advances nothing past a checkpoint. Automatic advancement still runs only the two draft steps (013 classification), not widened. |
| IV. Typed auditable records | Pass. No record, field, enum value or layout change. The draft and its `DraftGenerated` audit entry keep their shape; only `StatusBefore`/`StatusAfter` can now be `Approved` for a requirements draft. |
| V. Spec-driven | Pass. Spec (with the missing-value edge case clarified), plan and tasks approved by the user before code. |
| VI. Test-first | Pass. Tests are written, run red and approved (T014) before any source file changes. No live model call. |
| VII. Simplicity | Pass. No new agent, type, helper or abstraction; two lines reuse `IChangeRequestService.GetById` in each of two endpoints. |
