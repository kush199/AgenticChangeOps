# Data Model: Automatic Advancement

All additions are additive: new optional members default to null, new types are new, new enum values
are appended. Nothing existing is renamed, reordered or removed.

## Changed existing types

| Type | Change | Notes |
|---|---|---|
| `ChangeRequestAuditEntry` | `+ bool? Automatic = null` (last member) | `true` only for a step run automatically; `null` for every manual entry and for all entries stored before this slice. |
| `WorkflowStatus` | `+ AutomaticAdvancementStatus? Automatic = null` (last member) | `null` only when no status source is wired (existing unit tests). Included in `Equals` and `GetHashCode` like every other member; stage, next step and reason are untouched. |
| `AdvanceOutcome` | `+ Busy` (appended) | Returned only to the automatic runner. The manual endpoint never receives it. |
| `IRequirementsDraftingService.DraftAsync` / `IDesignDraftingService.DesignAsync` | `+ bool automatic = false` | Passed through to the audit entry. |
| `IChangeRequestStore` | `+ ListChangeRequestIds`, `GetAutomaticAttempt`, `SaveAutomaticAttempt`, `ClearAutomaticAttempt` | Both stores implement them; the shared contract suite covers both. |

## New types

### `AutomaticAdvancementSettings` (immutable)

| Field | Type | Default | Valid range |
|---|---|---|---|
| `Enabled` | bool | `false` | `true` only on an explicit boolean `true` |
| `Interval` | TimeSpan | 5 min | 1 s – 1 day |
| `MaxStepsPerCheck` | int | 5 | 1 – 1000 |
| `FailureWait` | TimeSpan | 15 min | 0 s – 1 day |
| `MaxConsecutiveFailures` | int | 3 | 1 – 100 |

Built by `FromConfiguration(getValue, warn)`; an invalid value replaces that field only and calls
`warn` with the setting key (never the value).

### `AutomaticAttempt` (typed record, mutable bookkeeping)

| Field | Type | Meaning |
|---|---|---|
| `ChangeRequestId` | Guid | Key. One row per request. |
| `Step` | `WorkflowStep` (stored in the Intake record as its enum name, a string, because Intake cannot reference Workflow; mapped back by the Workflow code) | The step that failed. |
| `CompletedStepCount` | int | Completed steps (all drafts) when it failed; identifies "the current inputs". |
| `ConsecutiveFailures` | int ≥ 1 | Failures of this step on these inputs. |
| `LastFailureCategory` | `GenerationFailureCategory` (stored in the Intake record as its enum name, a string, for the same reason) | The only failure detail kept. |
| `LastFailureAt` | DateTime (UTC) | For the waiting time. |

Applies to the current situation iff `Step == NextStep` and `CompletedStepCount == completed.Count`;
otherwise it counts as zero failures. Deleted on a successful automatic step. Never an agent output,
never an audit entry, never carries text.

### `AutomaticAdvancementStatus` (in `WorkflowStatus`)

| Field | Type | Meaning |
|---|---|---|
| `Mode` | `Off` \| `On` \| `LeftForPerson` | `Off`: feature off. `LeftForPerson`: an applicable attempt row has `ConsecutiveFailures >= MaxConsecutiveFailures`. Otherwise `On`. |
| `FailureCategory` | `GenerationFailureCategory?` | Set only for `LeftForPerson`. |

`Off` takes precedence: with the feature off, no row is consulted.

### `CheckSummary`

`CheckedAt` (UTC), `Examined`, `Ran`, `Failed` — integers and time — plus
`FailuresByCategory` (`IReadOnlyDictionary<GenerationFailureCategory, int>`: only categories the agents
already report, with counts; an unexpected error counts in `Failed` but in no category). No string
members. The operator endpoint returns only the first four members; `FailuresByCategory` feeds the
log line. `CheckSummaryHolder` keeps the latest one under a lock; `null` until the first check since start.

## State transitions (per change request, automatic path)

```text
due & budget left & not busy & configured & (no applicable row | wait passed & failures < max)
   ├─ Advanced            → row deleted; Ran++
   ├─ StepFailed(category)→ row upserted (count+1, or 1 if not applicable); Failed++
   ├─ StepFailed(no cat.) → no change (facts changed mid-step)
   ├─ NothingDue / NotConfigured / Busy → no change
   └─ exception           → Failed++ (no row written)
applicable row with failures >= max → LeftForPerson: never attempted; shown in status
```

A manual advance never reads or writes attempt rows. A manual success changes the completed-step count,
so any old row stops applying by itself.

## Stored layout (SQLite, version 2)

Existing tables unchanged. New:

```text
automatic_attempts(
  change_request_id TEXT PRIMARY KEY,
  payload TEXT NOT NULL            -- the AutomaticAttempt record as JSON, like every other record
)
```

`user_version` becomes 2. A version-1 file with exactly its three tables is upgraded in one
transaction on first open; any other layout is refused as before.
