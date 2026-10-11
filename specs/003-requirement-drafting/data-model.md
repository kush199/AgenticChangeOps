# Data Model: Requirement Drafting for Change Requests

Extends the slice 001/002 model. All members are immutable records. New items are marked NEW;
changed items are additive only.

## RequirementsDraft (NEW)

| Member | Type | Notes |
|--------|------|-------|
| `DraftId` | `Guid` | From `IIdGenerator` |
| `ChangeRequestId` | `Guid` | Links to the change request; must exist |
| `Version` | `int` | 1-based, per change request, no gaps, never reused |
| `GeneratedAt` | `DateTime` | UTC, from `ISystemClock` |
| `Status` | `DraftStatus` | Always `Draft` in this slice |
| `AgentName` | `string` | Constant `"RequirementAgent"` |
| `Summary` | `string` | Trimmed, non-empty, ≤ 2,000 chars |
| `FunctionalRequirements` | `IReadOnlyList<string>` | ≥ 1 item, ≤ 50, each ≤ 2,000 chars |
| `AcceptanceCriteria` | `IReadOnlyList<string>` | ≥ 1 item, ≤ 50, each ≤ 2,000 chars |
| `Assumptions` | `IReadOnlyList<string>` | 0–50 items |
| `OpenQuestions` | `IReadOnlyList<string>` | 0–50 items |

Immutable once stored. Never updated or removed by this slice.

## DraftStatus (NEW enum)

`Draft`. Later values are appended at the end in future slices (additive).

## DraftContent (NEW)

Raw generator output, before validation. Carries no identity, version, or timestamp.

| Member | Type |
|--------|------|
| `Summary` | `string?` |
| `FunctionalRequirements` | `IReadOnlyList<string>?` |
| `AcceptanceCriteria` | `IReadOnlyList<string>?` |
| `Assumptions` | `IReadOnlyList<string>?` |
| `OpenQuestions` | `IReadOnlyList<string>?` |

`null` = section absent (error). Empty list = present but empty (allowed only for
`Assumptions` and `OpenQuestions`).

## DraftingResult (NEW)

Same shape as `DecisionResult`: `IsSuccess`, `Draft` (`RequirementsDraft?`), `Errors`
(`IReadOnlyList<ValidationError>`). Exactly one of draft or errors is populated.

## QueryResult&lt;T&gt; (NEW)

`IsSuccess`, `Value` (`T?`), `Errors`. Used by `GetLatestDraft` (`T = RequirementsDraft?`, value
null when no draft exists) and `GetDraftHistory` (`T = IReadOnlyList<RequirementsDraft>`).

## ChangeRequestAuditEntry (MODIFIED, additive)

| New member | Type | Notes |
|------------|------|-------|
| `AgentName` | `string?` | Trailing, default `null`; `"RequirementAgent"` for drafting entries |
| `DraftVersion` | `int?` | Trailing, default `null`; the version stored |

New `AuditEventType` value `DraftGenerated`, appended last. For drafting entries:
`EventType = DraftGenerated`, `OccurredAt` = the draft's `GeneratedAt`, `StatusBefore` and
`StatusAfter` both equal the unchanged request status, `ApproverName`/`Reason`/`Details` null.

## ChangeRequest (unchanged)

Read only by this slice. `Status` is never modified by drafting.

## Validation rules (from the spec)

| Rule | Source | Error `FieldName` |
|------|--------|-------------------|
| Request exists | FR-004 | `ChangeRequestId` |
| Status is `Submitted` or `Deferred` (checked before and after generation) | FR-003/005 | `Status` |
| Generator did not fail or return null | FR-016 | `Generator` |
| Trim items; drop blank items | Clarification | n/a |
| `Summary` present and non-empty | FR-007 | `Summary` |
| `FunctionalRequirements`, `AcceptanceCriteria` present with ≥ 1 item | FR-007 | section name |
| `Assumptions`, `OpenQuestions` present (may be empty) | FR-007 | section name |
| Item ≤ 2,000 chars; list ≤ 50 items | FR-022 | section name |
| Report every failing section | FR-017 | one error each |

## Flow

```text
DraftAsync(id)
  ├─ request missing?              → error ChangeRequestId      (generator not called)
  ├─ status not Submitted/Deferred → error Status               (generator not called)
  ├─ generator throws / null       → error Generator            (nothing stored)
  ├─ normalise + validate          → errors per section         (nothing stored)
  └─ lock: re-check status → version = count + 1 → SaveDraft(draft, audit) → success
```
