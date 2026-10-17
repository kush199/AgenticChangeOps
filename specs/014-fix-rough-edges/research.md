# Research: Fix Three Rough Edges

No unknowns remained in the Technical Context. These are the design decisions.

## 1. Message wording

- **Decision**: One wording for all three fields, built from the field name and the accepted names
  only: `<Field> is not a recognised value. Accepted values: <list>.` The lists are the existing
  ones (`Low, Medium, High, Critical`; `Feature, Bugfix, ConfigChange`; `Approve, Reject, Defer`).
  The rejected value is not interpolated.
- **Rationale**: The cause of the placeholder is that the API maps every unrecognised text to the
  out-of-range value `-1` and the validators print it. Dropping the interpolation fixes the
  placeholder and the number, and also means caller text can never be echoed, because the validators
  never see it. The existing tests only check that the accepted names appear, so they keep passing.
- **Alternatives**: Keep the value but quote the original text (rejected: echoes caller text, which
  the spec forbids). Carry the original text through the API and sanitise it (rejected: new plumbing
  to protect something we do not want to show).

## 2. Where to let an Approved request be drafted

- **Decision**: Change `IsDraftable` so Approved counts only when the request has no requirements
  draft, checked in both places eligibility is checked (before the model call, and again inside the
  store lock before the save). `WorkflowRules.Derive` returns the requirements draft as the next
  step for that case, keeping stage `AwaitingRequirementsDraft`.
- **Rationale**: The workflow, manual advance and the automatic check all derive what is due from
  `WorkflowRules` and run it through `RequirementsDraftingService`. Fixing the two rules fixes all
  three paths and nothing else has to know. The second check inside the lock preserves the existing
  one-version-at-a-time guarantee and makes "only when none exists" safe against a concurrent draft.
- **Consequence to state**: a request approved while its first draft is being generated now keeps
  that draft (previously the save was refused). This follows directly from FR-003 and is covered by
  the changed `RequestBecomesApprovedDuringGeneration` test.
- **Alternatives**: Run a draft inside the approval operation (rejected: approval must not change,
  FR-006). A new "late draft" step or stage (rejected: extra concept, changes the status shape).

## 3. Order of checks on the two drafting operations

- **Decision**: In both endpoints, after the identifier parse, call `IChangeRequestService.GetById`;
  if it returns null respond `ErrorMapper.NotFound()`; then check availability.
- **Rationale**: `GetClearance` already does exactly this, and `ErrorMapper.ForDrafting` /
  `ForDesigning` already turn the services' not-found into `NotFound()`, so the unknown-identifier
  response is identical whether or not drafting is configured. A duplicate lookup when configured is
  one in-process read; the service still does its own check.
- **Alternatives**: Move the availability test into the services (rejected: they have no knowledge
  of configuration and it would add a dependency). Check availability first and look up inside
  (rejected: that is the current behaviour).

## 4. Proving "nothing else changed"

- **Decision**: Rely on the recorded golden transcripts (`transcript.txt`,
  `design-operations-transcript.txt`, `openapi.json`) and on the full existing suite. The golden
  diff must touch only the lines listed in the plan; the OpenAPI document must not change at all
  (no route, status code or schema changes).
- **Alternatives**: A separate before/after harness (rejected: the baselines already are that).
