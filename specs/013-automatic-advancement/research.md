# Research: Automatic Advancement

No `NEEDS CLARIFICATION` remained in the plan. These are the design decisions, each with the
alternatives considered.

## R1. Where the check logic lives

- **Decision**: `AutomaticAdvancementService.RunCheckAsync` in `AgenticChangeOps.Workflow`, driven by
  `ISystemClock`, the store, `IWorkflowService.GetStatus`, and `IAutomaticStepRunner`. A thin
  `BackgroundService` in the Api project calls it on a `PeriodicTimer`.
- **Rationale**: the rules, the gate and the agent services are already in Workflow. Tests call
  `RunCheckAsync` with a fake clock and fake generators: no timers, no sleeping. Workflow stays free of
  ASP.NET.
- **Alternatives**: the loop inside the Api project (untestable without timers, and logic ends up in
  hosting code); a new project (not needed, Principle VII).

## R2. Reusing the manual path and its gate

- **Decision**: `WorkflowService` implements a second interface, `IAutomaticStepRunner.TryRunAsync`.
  It uses the same per-request `SemaphoreSlim` but with `Wait(0)`: if the gate is busy it returns
  `AdvanceOutcome.Busy` (skip, not a failure). Otherwise it re-derives inside the gate and runs the step
  exactly like `AdvanceAsync`, passing `automatic: true` to the agent service.
- **Rationale**: one gate means one step at a time across automatic and manual. Manual `AdvanceAsync` is
  untouched: it still waits and then re-derives, so spec 010's "second advance reports nothing due"
  keeps holding and no existing response changes.
- **Alternatives**: making manual advances fail fast when busy (changes existing behaviour); a second
  lock (does not exclude).

## R3. What counts as "due" and which request goes first

- **Decision**: due = `WorkflowStatus.NextStep` from `WorkflowService.GetStatus`, i.e. `WorkflowRules`.
  Requests are examined in the order they were stored (`ListChangeRequestIds`). The budget counts
  attempts (steps run plus steps failed). Requests that are waiting out a failure, left for a person,
  not configured, busy, or not due cost nothing.
- **Rationale**: a request that succeeds stops being due, and a failing one is skipped until its wait
  passes, so the head of the queue always moves and later requests are not starved (FR-022). The order
  is deterministic and needs no model.
- **Alternatives**: random order (not testable); least-recently-attempted (needs more state for the
  same result).

## R4. Attempt record and what "same step" means

- **Decision**: one row per change request: `(ChangeRequestId, Step, CompletedStepCount,
  ConsecutiveFailures, LastFailureCategory, LastFailureAt)`. A row applies to the current situation only
  if its `Step` equals the next step and its `CompletedStepCount` equals the current number of completed
  steps. Otherwise it is treated as zero failures. A successful automatic step deletes the row.
- **Rationale**: any new draft (automatic or manual) changes the completed-step count and any decision
  changes the next step, so "a person succeeded" and "a different step is due" (FR-014, US3-5) reset
  the count with no extra bookkeeping. Manual success never has to touch the row.
- **Alternatives**: counting failures per draft version (does not cover a requirements change that makes
  a design stale); clearing rows from the manual path (touches the existing path).

## R5. What is a failure

- **Decision**: a step result of `StepFailed` **with a failure category** (model failure, timeout,
  malformed output, and the other categories the agents report) increments the count. `StepFailed`
  without a category (eligibility changed while the model ran, for example a decision was recorded) is
  not counted: the facts changed and the next check re-derives. `NotConfigured` is a skip. An unexpected
  exception (for example storage unavailable) counts as one failed step in the summary with a fixed
  category; no row can be written then and nothing is logged beyond the category.
- **Rationale**: matches FR-012/FR-015 and never turns a person's decision into a "failure".

## R6. Persisting attempts (FR-016)

- **Decision**: new table `automatic_attempts`, layout version 2. `Prepare` upgrades a version-1 file in
  place in one transaction (create table, set `user_version = 2`); version 0 with no tables creates v2
  directly; anything else is still `UnsupportedLayout`. Attempt rows hold counts, a category and a time
  only.
- **Rationale**: keeps failures out of drafts and out of the audit history (which would change an
  existing response) while surviving restart. Layout versioning already exists for exactly this.
- **Alternatives**: audit entries for failures (changes audit listing, contradicts "stores nothing");
  a sidecar file (second durability mechanism); memory only (loses state on restart, violating FR-016).

## R7. Marking automatic steps in the audit trail (FR-017)

- **Decision**: `ChangeRequestAuditEntry` gets `bool? Automatic = null`, set to `true` only by an
  automatic step; manual entries keep `null`. The two agent services accept an optional
  `automatic` argument and stamp it while building the entry they already build.
- **Rationale**: the entry is already written atomically with the draft, so the marker cannot be lost or
  disagree. `null` for manual means existing stored entries and manual output are byte-for-byte the same
  as before in stored form; the audit endpoint adds an `automatic` member (null) which is additive.
- **Alternatives**: a separate "origin" table (second source of truth); a different event type (breaks
  existing consumers that switch on it).

## R8. Status additions (FR-018)

- **Decision**: `WorkflowStatus` gets `AutomaticAdvancementStatus? Automatic = null`, with
  `Mode` = `Off` | `On` | `LeftForPerson` and `FailureCategory?` (set only for `LeftForPerson`).
  `WorkflowService` takes an optional status source (settings + attempt lookup). When absent (existing
  tests) `Automatic` stays null. `WorkflowRules` stays pure and unchanged.
- **Rationale**: the stage and next step are still derived only from recorded facts; the new block is
  derived from settings and attempt rows and cannot change `NextStep` or `Reason`. Manual advance
  ignores it entirely (a request left for a person can still be advanced by a person).
- **Alternatives**: changing `WorkflowRules` (breaks its purity and its tests); a separate endpoint
  (spec says the reason is in the workflow status).

## R9. Settings and validation

- **Decision**: read through `IConfiguration` like the model settings (`ChangeOps:AutoAdvance:*`).
  `Enabled` is true only for a value that parses as a boolean `true`; anything else, including invalid,
  is off. Numbers must be positive integers within a ceiling (interval 1 s to 1 day, steps 1 to 1000,
  wait 0 s to 1 day, failures 1 to 100); an invalid number falls back to its default for that setting
  alone. A warning names the setting key and says the default is used, never the value.
- **Rationale**: spec clarification Q3; a bad value never stops the host starting or turns the feature
  on by accident.
- **Read once**: settings are read at start-up only; switching the feature off needs a restart (spec
  edge case). Re-reading each tick was rejected as extra moving parts for no stated need.

## R10. Timer behaviour

- **Decision**: when disabled, the hosted service returns at once and creates no timer. When enabled it
  runs a check immediately at start (so a restart catches up from recorded facts) and then once per
  interval using `PeriodicTimer`, which cannot overlap itself because the next tick is awaited only after
  the check returns. Per-check exceptions are caught, counted and logged by type name only; the loop
  continues. Stopping the host cancels the token; an in-flight model call is cancelled like any request.
- **Rationale**: FR-021, and "a restart loses nothing and repeats nothing" (completed steps are never
  due again).

## R11. Operator summary (clarification Q1)

- **Decision**: `GET /operator/automatic-advancement` returns `{ enabled, lastCheck }`; `lastCheck` is
  `null` until a check has run since start, otherwise `{ checkedAt, examined, ran, failed }`. The same
  four facts are logged as one line per check. The holder keeps only the latest summary, in memory.
- **Rationale**: counts and times cannot carry model text, an endpoint, a deployment name or a
  credential by construction (no string member besides fixed field names).

## R12. Overlap with direct draft endpoints

- **Decision** (confirmed with the user after the red tests showed the original assumption was wrong):
  no change to the direct operations. `POST …/drafts` and `…/designs` are the agents' own operations,
  not workflow steps, and they do not take the workflow gate (they never did). The drafting services
  check a request's status inside the store lock but not whether a draft already exists, so two direct
  calls already store two versions. An automatic step that overlaps a direct call can therefore also
  end with two versions. The "one step at a time, exactly one new draft" guarantee covers workflow
  advances (manual and automatic), which share the per-request gate.
- **Rationale**: closing it means routing the direct endpoints through the gate, which changes existing
  endpoint code and behaviour outside this slice. The earlier claim that the store-lock check
  makes overlap with a direct call safe was incorrect.
- **Alternatives**: route the direct endpoints through the gate (rejected for this slice; could be its
  own slice if wanted); make the drafting services refuse a draft when one is already current
  (changes the agents' re-draft behaviour).
