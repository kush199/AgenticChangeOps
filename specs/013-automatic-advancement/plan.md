# Implementation Plan: Automatic Advancement of Change Requests

**Branch**: `feature/automatic-advancement` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013-automatic-advancement/spec.md`

## Summary

Add an opt-in background check that, every operator-set interval, lists the stored change requests,
asks the existing workflow rules what is due, and runs at most N due steps through the **same
workflow service and the same per-request gate** the manual advance uses. Nothing about what is due
or what runs is new: `WorkflowRules.Derive` decides, `WorkflowService` runs the Requirement or
Architect service, and those services write the draft and audit entry exactly as today. The only new
things are:

- a check loop and its settings (off by default);
- a small per-request **attempt record** (consecutive failures, last category and time) so the
  waiting time and hand-over survive a restart;
- an additive **automatic marker** on the audit entry of a step run automatically;
- an additive **automatic** block on the workflow status (`Off` / `On` / `LeftForPerson`);
- a read-only operator endpoint with the latest check summary, plus one log line per check.

Everything else (drafts, approvals, errors, endpoints) is unchanged. See [research.md](research.md)
for the decisions and their alternatives.

## Technical Context

**Language/Version**: C# on .NET 8 (`net8.0`, as every existing project)

**Primary Dependencies**: none new. ASP.NET Core hosting (`BackgroundService`, `PeriodicTimer`) already comes with the Api project; `Microsoft.Data.Sqlite` already in Persistence.

**Storage**: existing SQLite file, layout version 1 → 2 (adds one table, `automatic_attempts`; a version-1 file is upgraded in place, a newer or foreign layout is still refused). In-memory store gains the same members for tests.

**Testing**: xUnit; fakes for the model generators and the clock; the shared store contract suite gains the new members; real temp files for the layout upgrade and restart tests. No live model call.

**Target Platform**: Local Windows/Linux process, single running copy

**Project Type**: Existing multi-project web-service solution (no new project)

**Settings** (read once at start-up; switching the feature off takes effect on restart) (`ChangeOps:AutoAdvance:*`, env `ChangeOps__AutoAdvance__*`): `Enabled` (default `false`), `IntervalSeconds` (300), `MaxStepsPerCheck` (5), `FailureWaitSeconds` (900), `MaxConsecutiveFailures` (3). Invalid value → that value's default, logged by setting name only.

**Performance Goals**: none beyond "a check over thousands of requests finishes well inside the interval"; one status derivation per request per check.

**Constraints**: deterministic due-ness (no model); checks never overlap; at most one step per request at a time; no model text, endpoint, deployment name, credential, path or driver message in any summary, status, audit entry, log or error; no existing response changes meaning.

**Scale/Scope**: portfolio scale, one process, thousands of requests.

## Constitution Check

*Constitution v1.0.4.*

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. All fixtures synthetic; settings contain no secrets; nothing new is committed. |
| II. Deterministic routing | Pass, and the core of this slice. What is due, in what order and when to stop is plain C# over recorded facts (`WorkflowRules`, attempt counts, the clock). The model only generates draft content inside the existing agent services and is never asked what to run. |
| III. Human approval gates | Pass, relying on the explicit classification in the spec: the requirements draft and the design draft are **non-consequential** (draft content only; no status change, decision or clearance). The classification applies to those two steps only and is to be confirmed in the compliance review. No decision, approval, deferral, rejection or clearance is ever made automatically; Approved/Rejected/Deferred still need a recorded human decision. |
| IV. Typed auditable records | Pass. Automatic steps write the same typed draft and audit entry. Additive changes only: new nullable `Automatic` member on `ChangeRequestAuditEntry` (null for manual), new nullable `Automatic` member on `WorkflowStatus`, new typed `AutomaticAttempt` record. Existing constructions, stored entries and serialized values stay valid. **Interpretation to confirm at review**: the attempt record is operational bookkeeping, not an agent input/output or audit record, so Principle IV's minimum fields (`AgentName`, `InputPayload`, `OutputPayload`, `ApprovalStatus`) do not apply to it; it is never referenced as an upstream record version, never replaces a draft or audit entry, and is kept apart from both. A new table with an in-place layout upgrade is treated as additive because no existing record type, entry or value changes. |
| V. Spec-driven | Pass. Spec and clarifications done; this plan, then tasks, must be approved before code. |
| VI. Test-first | Pass. Deterministic logic (due selection, budget, wait, hand-over, settings validation, overlap) gets tests written, shown red and approved before implementation. All model access stays behind the existing generator interfaces with fakes. |
| VII. Simplicity | Pass with tracked items (below). Still four agents; no new agent, project or framework. |
| Tech stack | Pass. No new technology; Agent Framework not used for orchestration (out of scope); SQLite behind `IChangeRequestStore`; the in-memory store stays and the shared contract suite runs against both. |

No violations. **Post-design re-check**: unchanged. The design adds no model call, no routing by a
model, and no automatic decision.

## Project Structure

### Documentation (this feature)

```text
specs/013-automatic-advancement/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── operator-summary.md
│   └── workflow-status-additions.md
└── tasks.md             # /speckit-tasks, not created here
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Intake/
│   ├── Models/ChangeRequestAuditEntry.cs        # + bool? Automatic = null (additive)
│   ├── Models/AutomaticAttempt.cs               # NEW typed record (id, step, completed-count, failures, category, time)
│   └── Stores/IChangeRequestStore.cs            # + ListChangeRequestIds, GetAutomaticAttempt, SaveAutomaticAttempt, ClearAutomaticAttempt
│       └── InMemoryChangeRequestStore.cs        # + the same members
├── AgenticChangeOps.Agents.Requirement/
│   ├── IRequirementsDraftingService.cs          # DraftAsync(..., bool automatic = false)
│   └── RequirementsDraftingService.cs           # stamps Automatic = true on the audit entry only when asked
├── AgenticChangeOps.Agents.Architect/
│   ├── IDesignDraftingService.cs                # DesignAsync(..., bool automatic = false)
│   └── DesignDraftingService.cs                 # same
├── AgenticChangeOps.Workflow/
│   ├── WorkflowStatus.cs                        # + AutomaticAdvancementStatus? Automatic = null (additive)
│   ├── AutomaticAdvancementStatus.cs            # NEW: Mode (Off/On/LeftForPerson) + FailureCategory?
│   ├── AutomaticAdvancementSettings.cs          # NEW: typed settings, defaults, validation
│   ├── IAutomaticStepRunner.cs                  # NEW: TryRunAsync (skips when the request's gate is busy)
│   ├── AutomaticAdvancementService.cs           # NEW: RunCheckAsync → CheckSummary (the deterministic core)
│   ├── CheckSummary.cs, CheckSummaryHolder.cs   # NEW: counts + times only; latest, in memory
│   ├── AdvanceOutcome.cs                        # + Busy (appended; manual never returns it)
│   └── WorkflowService.cs                       # + IAutomaticStepRunner; optional status source adds Automatic
├── AgenticChangeOps.Persistence/
│   ├── StorageSchema.cs                         # layout 2, + automatic_attempts table, 1→2 upgrade
│   └── SqliteChangeRequestStore.cs              # + the new members, upgrade in Prepare
└── AgenticChangeOps.Api/
    ├── Composition/ApiServiceRegistration.cs    # settings, service, holder, hosted service
    ├── Composition/AutomaticAdvancementHostedService.cs   # NEW: timer loop; returns at once when off
    └── Endpoints/OperatorEndpoints.cs           # NEW: GET /operator/automatic-advancement

README.md                                        # settings table, endpoint, safety notes

tests/
├── AgenticChangeOps.Workflow.Tests/Automatic/…   # due selection, budget, wait, hand-over, overlap, no-leak, structure
├── AgenticChangeOps.Persistence.Tests/…          # contract: attempt members, listing order; 1→2 upgrade; restart
├── AgenticChangeOps.Agents.*.Tests/…             # automatic marker only when asked; default unchanged
└── AgenticChangeOps.Api.Tests/Automatic/…        # off by default, operator endpoint, status additions, existing-response guard
```

**Structure Decision**: No new project. The deterministic core (`AutomaticAdvancementService`) lives
in `AgenticChangeOps.Workflow` beside the rules it reuses and is driven by a clock and a runner
interface, so tests call `RunCheckAsync` directly with no timer. Only the thin timer loop sits in
the Api project (hosting is an Api concern; Workflow stays free of ASP.NET). `AgenticChangeOps.Intake`
stays dependency-free.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `IChangeRequestStore.ListChangeRequestIds` | Nothing can enumerate stored requests today (only `FindById`); a periodic check must. One caller (the check), named here so the compliance review sees it. It is an interface member replacing no existing one, not a generic abstraction. | Scanning drafts or audit rows is not a list of requests (a new request has neither); a second "index" store would duplicate the store. |
| Three attempt members (`Get/Save/ClearAutomaticAttempt`) and `AutomaticAttempt` record, layout 1→2 | FR-016: waiting time, failure count and hand-over must survive restart and be kept apart from drafts and audit. Two callers: the check and the status source | In-memory counts lose state on restart (violates FR-016). Failure audit entries would appear in the audit history (a change to an existing response) and contradict "a failed step stores nothing". |
| `automatic` optional parameter on the two agent service methods | The marker must be written into the same atomic audit entry the step already writes; it cannot be added afterwards | Stamping the entry after the fact breaks atomicity. An ambient (async-local) flag hides the coupling. Fakes implementing the two interfaces need the extra optional parameter (compile-only edit, listed in tasks). |
| `IAutomaticStatusSource` (one implementation, `AutomaticStatusSource`; one caller, `WorkflowService`) | `WorkflowService` must show `Off` / `On` / `LeftForPerson` without referencing settings or attempt storage directly; the interface keeps `WorkflowService`'s constructor additive (optional parameter, null in existing tests) so every existing test constructs it unchanged. Replaces no existing member | Passing settings and the store into `WorkflowService` would change its constructor for every existing caller and mix bookkeeping into the class that runs steps |
| `AdvanceOutcome.Busy` and `IAutomaticStepRunner` | Automatic attempts must skip, not wait, when a person's advance holds the request's gate | Reusing `AdvanceAsync` would block the whole check behind one slow request. A second gate would not exclude manual runs. |
| Additive `Automatic` member on `WorkflowStatus` and on `ChangeRequestAuditEntry` | Spec FR-017/018 require the visible markers | Separate endpoints would not put the reason "in its workflow status" as required. Both are nullable and default null; existing constructions stay valid. |
| Layout upgrade path (v1 file → v2) | A user's existing database must keep working | Refusing v1 files would lose stored data; a fresh-file-only change would break upgraders. |

## Notes on risks (for review)

- Existing store/layout tests that pin "version 1 is current" must change to "2 is current and 1 is
  upgraded"; tests that use 2 as a "future, unsupported" version must move to a higher number. These
  are necessary edits to existing tests and will be listed in tasks for explicit approval.
- Direct draft endpoints (`POST …/drafts`, `…/designs`) bypass the workflow gate and are outside the workflow step (research R12, corrected during implementation). The guarantee of one step at a time and exactly one new draft covers workflow advances, manual and automatic. A direct call that overlaps an automatic step can store a second version, as two direct calls already can.
- The status response gains an `automatic` object on every workflow status and advance response.
  This is the additive change the spec itself asks for (FR-018); no existing member changes.

## Constitution compliance check (pre-code), v1.0.4, 2026-10-16

| Principle | Result |
|---|---|
| I. Synthetic data only | Pass. Synthetic fixtures; leaky-marker test strings are fake (`*.example.invalid`, `sk-synthetic-0000`). |
| II. Deterministic routing | Pass. Due-ness, order, budget, waiting and hand-over are plain C# over recorded facts and the clock; no model call decides anything. |
| III. Human approval gates | Pass. FR-025 classifies the requirements and design draft steps as non-consequential, only for that reason; no decision, approval or clearance is automatic. Classification confirmed for review. |
| IV. Typed auditable records | Pass, with the stated interpretation: the attempt record is bookkeeping outside the agent-record minimum fields; changes are additive. |
| V. Spec-driven | Pass. Spec, clarifications, plan, analysis edits and tasks approved by the user before code. |
| VI. Test-first | Pass. Tests are written and shown red, and approved (T034), before any stub is filled in. Model access stays behind existing interfaces with fakes. |
| VII. Simplicity | Pass. New interfaces and store members are listed in Complexity Tracking. Still four agents. |

## Constitution compliance check (post-implementation), v1.0.4, 2026-10-16

| Principle | Result |
|---|---|
| I. Synthetic data only | Pass. All fixtures synthetic; leaky-marker strings are fake (`*.example.invalid`, `sk-synthetic-0000`); no setting value, endpoint or secret is committed or logged. |
| II. Deterministic routing | Pass. Due-ness comes from `WorkflowRules`; order is storage order; budget, waiting and hand-over are plain C# over recorded facts and the clock. No model call decides anything; `AutomaticAdvancementService` names no model, transport or logging type (structure tests). |
| III. Human approval gates | Pass. Only the two draft steps (FR-025, non-consequential) run unasked. The service never builds a draft, audit entry or decision, never calls the approval service, and never changes a status (tests assert statuses and decision entries are unchanged). |
| IV. Typed auditable records | Pass. Automatic steps write the same typed draft and audit entry; the only record changes are additive nullable members (`Automatic` on the audit entry and on `WorkflowStatus`, omitted from JSON when null) plus the new `AutomaticAttempt` bookkeeping record (outside the agent-record minimum fields, as stated in the plan). Manual entries and stored entries are unchanged. |
| V. Spec-driven | Pass. Spec, clarifications, plan, analysis edits and tasks approved before code; red tests approved (T034). |
| VI. Test-first | Pass. Tests written and shown red (`red-run-T033.txt`) and approved before any stub was filled in. Model access stayed behind the existing generator interfaces with fakes; no live model call. |
| VII. Simplicity | Pass. New interfaces and store members match Complexity Tracking. Still four agents; no new project, package or framework. |

### Deviations from the plan (all recorded in tasks.md "Implementation notes")

- Existing-test edits, all necessary and approved or flagged: `LayoutVersionTests` (current layout is 2); `StorageErrorTests.ThrowingStore` (four members); `ChangeRequestStoreContract` (cases C14 to C18); `DescriptionTests` (route list gains `GET /operator/automatic-advancement`, name now "Fourteen"); the generated golden `Baseline/golden/openapi.json` (93 added lines, 0 removed).
- `src/AgenticChangeOps.Api/appsettings.json` gained one logger-category level (`Information` for the hosted service) so the per-check log line is visible under the shipped `Warning` default.
- New file `src/AgenticChangeOps.Api/Contracts/OperatorSummaryResponse.cs` and `src/AgenticChangeOps.Workflow/AutomaticAttemptRules.cs` (shared "does this row apply" rule used by the check and the status source).
- A step that fails without a category (facts changed while the model ran) still uses one unit of the per-check budget, since a model call may have been made; it is not counted as failed.
- Research R12 corrected: a direct drafting call is outside the workflow step; the guarantee covers workflow advances.
