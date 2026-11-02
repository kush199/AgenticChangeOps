# Research: Design Acceptance Decision

## R1. Where the decision is stored
**Decision**: a `DesignDecision` record saved through `SaveAgentOutput<DesignDecision>` with `Version` equal
to the design version.
**Rationale**: the store already guarantees atomic output+audit save and rejects a second output of the same
type and version (in-memory throws, SQLite throws `StorageVersionConflictException`). That gives
exactly-one-decision with no new store method, and the SQLite store needs no schema change because outputs
are keyed by type name.
**Alternatives**: a new store method and table (schema change, layout bump, more code); a mutable decision
member on `DesignDraft` (breaks immutability and Principle IV).

## R2. Overlapping decisions
**Decision**: the service holds `store.SyncRoot` across read-latest, check-not-decided and save (the monitor
is re-entrant, as other services already rely on). The storage uniqueness rule is the backstop; a conflict
exception from it is mapped to the same "already decided" refusal.
**Rationale**: the loser gets the documented refusal rather than a 500, and nothing is stored twice.
**Alternatives**: rely on the storage exception alone (works, but the refusal would be an exception path).

## R3. Meaning of "latest" and "outdated"
**Decision**: a version is decidable only if it is the highest design version AND its `RequirementsVersion`
equals the latest requirements version from `IRequirementsBasisSource` (clarification Q1). Either failure is
the one "outdated" refusal.
**Rationale**: reuses the chain rule the workflow already uses for "current"; no new notion of staleness.

## R4. Check order
**Decision**: (1) change request exists (404); (2) status Approved (409); (3) version exists (404);
(4) version is latest and current (409 outdated); (5) not already decided (409); (6) field validation of
name, reason and decision value (400). Malformed id or version token are rejected at the route (400) before
the service.
**Rationale**: state refusals never depend on caller-supplied text, and a field error is reported only for a
request that could otherwise be recorded. "Stores nothing" holds at every step.
**Alternatives**: validate fields first, as the approval decision does; rejected because field errors would
mask which state refusal applies. The spec does not prescribe an order.

## R5. Shared validation limits
**Decision**: `DesignDecisionValidator` repeats the approval decision's rules by value (name required and
<= 100, reason <= 1000 after trimming, reason required for Reject) rather than referencing its private
constants.
**Rationale**: extracting the constants would touch an existing class for three numbers. A test asserts both
validators agree at the boundary lengths so they cannot drift.
**Alternatives**: extract shared constants (touches the approval slice for no behavioural gain).

## R6. Retrieval shape
**Decision**: `DesignView` mirrors `DesignDraft` member-for-member (same names, order and serialization
conditions) with a trailing `decision` member omitted when null. Both GET routes map through it.
**Rationale**: with no decision the JSON is identical to today; the only schema change is one optional
property. A test serializes a `DesignDraft` and a decision-less `DesignView` and asserts equal JSON.
**Alternatives**: merge into a `JsonNode` at runtime (loses the published schema for the new member).

## R7. Error codes
**Decision**: 404 `not-found` (unknown change request, existing), 404 `design-version-not-found`,
409 `conflict` (not Approved, existing shape), 409 `design-outdated`, 409 `design-already-decided`,
400 `invalid-identifier` (bad id), 400 `validation-failed` (bad version token, name, reason, decision).
The decision-value error lists `Accept, Reject` and never echoes the input.

## R8. Never automated
**Decision**: only `DesignEndpoints` references `IDesignDecisionService`. A structure test fails if
Workflow, the hosted advancement service or any agent project references it, mirroring the existing review
structure test.
