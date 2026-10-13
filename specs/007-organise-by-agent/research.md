# Research: Organise Agent Code by Agent

No `NEEDS CLARIFICATION` remain. Decisions below resolve the design questions the spec deferred to planning.

## R1. What is a "location"?

- **Decision**: A separate project (library) plus a matching test project.
- **Rationale**: A project boundary is the only mechanism that makes "must not refer to" and "must not depend on" compiler-enforced rather than a convention. It also makes "add one location beside the first" literal.
- **Alternatives considered**: Folders and namespaces inside one project. Rejected: nothing stops a neutral file from using an agent type.

## R2. Names

- **Decision**: `AgenticChangeOps.Agents.Requirement` (namespace `AgenticChangeOps.Agents.Requirement`), `AgenticChangeOps.ModelAccess` (namespace `AgenticChangeOps.ModelAccess`). Test projects add `.Tests`.
- **Rationale**: The agent project is named after the agent (FR-001). The shared project names no agent (FR-002) and says what it is. `Agents.` groups future agents beside the first.
- **Alternatives considered**: `AgenticChangeOps.Requirement` (no grouping for siblings); `AgenticChangeOps.Shared` (vague, invites unrelated helpers).

## R3. How does intake stop naming the draft type? (clarification Q1, option B)

- **Decision**: Intake gains `IAgentOutput { Guid ChangeRequestId; int Version; }` and two store methods, `SaveAgentOutput<T>(T output, ChangeRequestAuditEntry entry)` and `GetAgentOutputs<T>(Guid changeRequestId)`, both constrained to `IAgentOutput`. The in-memory store keeps outputs keyed by change request and concrete type, with the same rules as today: reject a mismatched change request id, reject a duplicate version, store output and audit entry together or neither, return ascending by version, all under `SyncRoot`.
- **Rationale**: Equivalent behaviour for the only concrete type in use. Typed, so Principle IV holds. `AuditEventType.DraftGenerated`, `AgentName` and `DraftVersion` on the audit entry are existing, agent-neutral strings, ints and enum values; their names and values stay (FR-012, FR-014).
- **Alternatives considered**: Option A, a store owned by the agent (splits an atomic save, risks lost-update behaviour); option C, keep drafts in intake (contradicts FR-004).

## R4. Keep existing call sites literal

- **Decision**: The agent project provides `SaveDraft(this IChangeRequestStore, RequirementsDraft, ChangeRequestAuditEntry)` and `GetDrafts(this IChangeRequestStore, Guid)` extension methods delegating to the generic methods.
- **Rationale**: Tests and the drafting service call `store.GetDrafts(id)` and `store.SaveDraft(d, e)` in dozens of places. Extension methods keep every assertion and call expression unchanged; only a `using` for the new namespace is added (FR-007).
- **Alternatives considered**: Rewriting call sites to `GetAgentOutputs<RequirementsDraft>(id)`. Rejected as unnecessary churn that risks the "assertions unchanged" rule.

## R5. Where do the failure carrier and categories go?

- **Decision**: `DraftGenerationException` and `GenerationFailureCategory` move to `ModelAccess`, names unchanged. `ModelFailureMapper`, the Azure client and the Azure options all depend on them, and the agent depends on them too.
- **Rationale**: ModelAccess must compile without Intake or the agent. The two types are used by the Azure client and the mapper and by no intake or approval code. Renaming would change existing test references and public surface, which FR-010 only allows with a listed reason; none is strong enough.
- **Residual**: The word "Draft" in `DraftGenerationException` is a vocabulary echo of the first agent, not a reference to it (no type, prompt or key). Recorded in `contracts/type-move-map.md` as a candidate rename for the slice that adds a second agent.
- **Alternatives considered**: Rename to `ModelCallException` now. Rejected: forces test and API edits beyond "moved" references and is not required by the rules.

## R6. Hygiene tests that name a folder

- **Decision**: `NoSecretsTests` (intake test project) hard-codes `src/AgenticChangeOps.Intake.AzureOpenAI` for its "no secret-like setting name" scan, and its file walker silently skips missing folders. Moving the code without updating the path would make that test pass vacuously. The test moves to `ModelAccess.Tests` with the folder constant pointing at `src/AgenticChangeOps.ModelAccess`, and a guard assertion that the scanned folder exists and yields at least one file is added in the same file (additive, not a changed assertion). The agent folder is also scanned.
- **Rationale**: Preserves the meaning of the guard (SC-001 plus Principle I).
- **Alternatives considered**: Leave the test where it is. Rejected: the intake test project would reference model access.

## R7. Test project split and shared fakes

- **Decision**: Tests that exercise the agent move to `Agents.Requirement.Tests`; tests of the neutral code move to `ModelAccess.Tests`; the rest stay. Small fakes used by both intake and agent tests (`FakeSystemClock`, `FakeIdGenerator`/counting generator, `FakeEnvironment` if needed) are shared by linked source includes, not copied and not by referencing the intake test project. `StoreTests` is split by method: draft-store tests move to the agent tests, intake-store tests stay. `StoreConcurrencyTests` moves to the agent tests because it drives the drafting service.
- **Rationale**: Intake tests must build without any agent or model project (US3). Linked includes avoid duplicated helper code and keep namespaces stable.
- **Alternatives considered**: A shared test-support project (new project for four tiny files; reconsider when a second agent has tests); copying fakes (drift).

## R8. Proving "no change in behaviour"

- **Decision**: Before moving anything, add `ApiBaselineTests` to `Api.Tests`. It drives all eight operations over success and every error path with the existing synthetic data and fakes, and compares each response's status, headers of interest, body and error code, plus the served API description, to golden files committed under `tests/AgenticChangeOps.Api.Tests/Baseline/`. Goldens are generated once on the pre-move code and reviewed. Volatile values (ids, timestamps) are normalised to stable tokens by the test, not by editing responses.
- **Test-only switch**: Setting `AGENTICCHANGEOPS_UPDATE_BASELINE=1` makes `ApiBaselineTests` rewrite the goldens instead of comparing. It is read only by that test, never by `src`, so it is not an application setting (FR-011). Use it only after reviewing an intended change; the default run only compares.
- **Rationale**: Gives a recorded before/after comparison (FR-008, FR-009, SC-002, SC-003) that outlives this slice.
- **Alternatives considered**: One-off manual capture and diff (not repeatable, not in CI); relying on existing tests alone (they assert selected fields, not full bodies and headers).

## R9. API description differences

- **Decision**: Expected difference list is empty. Schema names are the types' short names, so namespace and project moves do not change them. If the golden comparison shows any difference, it is listed in `contracts/http-api-unchanged.md` with its cause before the slice can close.
- **Rationale**: Honours "unchanged except where unavoidably affected, which must be listed".

## R10. Composition and registration

- **Decision**: `ApiServiceRegistration` keeps registering the same services in the same way; only `using` directives and project references change. A second agent adds one registration block there; neither agent project nor ModelAccess is edited.
- **Rationale**: The composition root is the one place allowed to know every agent.
