# Research: Requirement Drafting for Change Requests

All Technical Context items are resolved; no `NEEDS CLARIFICATION` remained after the spec
clarification session.

## 1. Production model adapter in this slice or not

- **Decision**: Ship only `IRequirementsDraftGenerator` and test fakes. Defer an Azure OpenAI
  adapter to a later slice.
- **Rationale**: The constitution requires LLM calls behind interfaces and forbids live calls in
  the test suite, so the adapter is untestable here by design. It would add a NuGet dependency
  and environment configuration to a library that currently has none, for code this slice cannot
  verify. The spec's testable behaviour (eligibility, validation, versioning, atomic storage,
  failure handling) is complete without it.
- **Alternatives considered**: Include the adapter now and exclude it from tests — rejected as
  unverified code in a public portfolio; include a deterministic "template" generator as the
  default — rejected as a speculative second implementation (Principle VII).
- **Follow-up**: When the adapter slice is specified, endpoint, deployment name, and key come
  from environment variables or Key Vault only (Technology Stack section; FR-021).

## 2. Sync or async generator

- **Decision**: `Task<DraftContent?> GenerateAsync(ChangeRequest, CancellationToken)`;
  `DraftAsync` on the service. Retrieval stays synchronous.
- **Rationale**: Model calls are network-bound and need cancellation and timeouts. Choosing
  async now avoids a breaking change to the abstraction later.
- **Alternatives considered**: Synchronous interface matching slices 001/002 — rejected for the
  breaking-change risk above.

## 3. How "missing section" is represented

- **Decision**: `DraftContent` members are nullable (`string?`, `IReadOnlyList<string>?`). Null
  means the section was absent; an empty list means present-but-empty. A null `DraftContent`
  returned from the generator is treated as wholly malformed.
- **Rationale**: Spec FR-007 distinguishes "absent" (always an error) from "present but empty"
  (allowed for assumptions and open questions). Non-nullable members could not express this.
- **Alternatives considered**: Non-nullable lists with empty meaning absent — rejected, loses
  the distinction the spec requires.

## 4. Error shape and codes

- **Decision**: Reuse `ValidationError(FieldName, Message)`. `FieldName` values:
  `ChangeRequestId` (not found), `Status` (not draftable), `Generator` (call failed or returned
  nothing), and the section names `Summary`, `FunctionalRequirements`, `AcceptanceCriteria`,
  `Assumptions`, `OpenQuestions` (missing, empty, or oversized).
- **Rationale**: Same collect-all pattern as slices 001/002; a caller distinguishes a
  generation failure (`Generator`) from a validation failure by field name. No new error type.
- **Alternatives considered**: An error-code enum — rejected as a speculative abstraction with
  one caller.

## 5. Generator failure handling

- **Decision**: Catch any exception from the generator (including cancellation and timeouts)
  and return a single `Generator` error. The message names the exception *type* but never its
  message text.
- **Rationale**: Exception messages from real clients can embed endpoints or request details;
  keeping them out of results avoids leaking configuration (FR-021 spirit). Nothing is stored
  and no version is consumed (FR-016).
- **Alternatives considered**: Letting exceptions propagate — rejected, FR-016 requires a
  specific returned error.

## 6. Version assignment and re-checking eligibility after the model call

- **Decision**: After the generator returns, enter a short synchronous section that (a)
  re-reads the change request and re-checks eligibility, (b) computes `version = count + 1`,
  and (c) stores draft and audit entry via one store call. The section is guarded by an
  instance lock.
- **Rationale**: The model call can take seconds; the request may be approved meanwhile, and
  two overlapping calls must not take the same version. Computing the version only after a
  valid draft exists guarantees no gaps (FR-009, US5 scenario 4).
- **Alternatives considered**: Reserve the version before calling the model — rejected, a
  failure would leave a gap.

## 7. Atomic draft + audit storage

- **Decision**: One store method `SaveDraft(draft, auditEntry)` stores both, with everything
  that can fail built beforehand (same pattern as slice 002's `ApprovalService`).
- **Rationale**: The spec edge case forbids a draft without its audit entry or vice versa.
- **Alternatives considered**: Two calls in sequence — rejected, partial state on failure.

## 8. Size limits

- **Decision**: After trimming and dropping blank items: summary ≤ 2,000 characters; each list
  item ≤ 2,000 characters; each list ≤ 50 items. Violations are errors, never truncation.
- **Rationale**: FR-022. Silent truncation would store content the model did not write.
