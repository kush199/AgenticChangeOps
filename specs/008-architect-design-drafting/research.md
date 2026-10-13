# Research: Architect Design Drafting

All unknowns from the Technical Context are resolved below; no `NEEDS CLARIFICATION` remains.

## R1. How the Architect reads the requirements draft without referencing the Requirement agent

- **Decision**: The Architect project declares `RequirementsBasis` (version plus the draft's summary, functional requirements, acceptance criteria, assumptions and open questions) and the port `IRequirementsBasisSource.GetLatest(Guid) : RequirementsBasis?`. The API composition root registers an adapter that calls the Requirement agent's `IRequirementsDraftingService.GetLatestDraft` and maps the result.
- **Rationale**: Slice 007 rule D6 and the Requirement/Architect structure tests require an agent project to reference only intake and model access. The port keeps the dependency pointing at the Architect's own type.
- **Alternatives considered**: (a) Project reference to `Agents.Requirement`: breaks D6. (b) Read `RequirementsDraft` via reflection or JSON: untyped, violates Principle IV. (c) Move the requirements read model to intake: edits intake and spreads agent shapes there.

## R2. Where the "has a requirements draft" check and version assignment happen

- **Decision**: Same pattern as `RequirementsDraftingService`: check eligibility, call the generator, validate, then, under `store.SyncRoot` with no await, re-check eligibility and basis, assign `version = existing designs + 1`, and save the design with its audit entry through `SaveAgentOutput<T>`.
- **Rationale**: Proven pattern; prevents duplicate or skipped versions under concurrency and prevents storing a draft if the request stopped being eligible during generation.
- **Alternatives considered**: Optimistic retry on version clash: more code, no benefit with the in-memory store.

## R3. The basis version recorded on the design

- **Decision**: The basis is read once, before generation, and the stored `RequirementsVersion` is the version that was given to the model. At the locked re-check the latest basis is read again; if it has changed from the one the model saw, the request is refused as stale rather than storing a design linked to a requirements version it was not based on.
- **Rationale**: FR-003 requires the link to be true. With the in-memory store and no editing a change is only possible by a concurrent requirements redraft, so refusal is rare and honest.
- **Alternatives considered**: Silently relink to the newer version: false lineage. Regenerate automatically: unbounded model calls.

## R4. Error vocabulary and HTTP mapping ("same conventions")

- **Decision**: Reuse the existing mapping rules by field name, in a new additive `ErrorMapper.ForDesigning`:
  - malformed id: 400 `invalid-identifier` (unchanged helper);
  - unknown id: 404 `not-found`;
  - status not Approved: 409 `conflict`, field `Status`;
  - no requirements draft (or stale basis): 409 `conflict`, field `RequirementsDraft`;
  - generator failure, timeout, oversize, malformed or incomplete output: 502 `generation-failed` with `category` only;
  - drafting not configured: 503 `design-not-configured`;
  - no design yet (latest): 404 `no-design-draft`.
- **Rationale**: Matches slice 005 status codes and body shape for equivalent situations; only the codes that name the artefact differ so a caller can tell the two drafts apart.
- **Alternatives considered**: Reusing `no-draft` and `drafting-not-configured` verbatim: messages would misname the artefact. Using 422 for missing requirements: breaks the convention that state refusals are 409.

## R5. Routes

- **Decision**: `POST /change-requests/{id}/designs` (201 with `Location` of `.../designs/latest`), `GET /change-requests/{id}/designs/latest`, `GET /change-requests/{id}/designs`.
- **Rationale**: Mirrors `/drafts`, `/drafts/latest`.
- **Alternatives considered**: `/design-drafts`: longer and inconsistent with the existing noun style.

## R6. Model call, prompt and output contract

- **Decision**: One `IChatModelClient.CompleteAsync` call, no retry, using `AzureOpenAIGeneratorOptions` (timeout, maximum characters) unchanged. The system instruction states that everything after a fixed delimiter is data. The user content is the delimiter plus a JSON object with the change request's own fields and the requirements basis. The model must answer with one JSON object with keys `approachSummary`, `affectedComponents`, `impactAndRisks`, `dependencies`, `rollbackConsiderations`, `openQuestions`. `approachSummary` is a string; the rest are lists of strings. Required non-empty: `approachSummary`, `affectedComponents`, `impactAndRisks`, `rollbackConsiderations`. `dependencies` and `openQuestions` may be empty lists but must be present.
- **Rationale**: Mirrors the proven requirements contract; the required set reflects the spec's "incomplete" rule while allowing genuinely none dependencies or questions.
- **Alternatives considered**: Free text sections: no deterministic validation. Streaming: unnecessary.

## R7. Effect on existing golden and baseline tests

- **Decision**: The API baseline test snapshots the OpenAPI description and a transcript. New routes add paths to `openapi.json` and the description string changes, so that golden is regenerated and the diff is reviewed. Allowed changes: new paths, new schemas, new codes in the description text, and exactly one new value (`5`) in the existing `AuditEventType` enum schema. Any change to an existing path, schema member, status code or existing enum value fails the review. The transcript golden is unaffected unless the audit enum list is printed; if so, only the appended value shows.
- **Rationale**: The golden proves existing behaviour is unchanged; an additions-only diff (as defined above) is the check.
- **Alternatives considered**: Excluding new routes from the baseline: hides regressions.

## R8. Live check

- **Decision**: One opt-in live test in the Architect test project, gated by the same environment switch and attribute pattern as the Requirement agent's live check, asserting only that a well-formed draft with all sections comes back for a synthetic request. Never part of the default run.
- **Rationale**: Spec FR-015; Principle VI.
- **Alternatives considered**: No live check: leaves prompt and parser untested against a real model.

## R9. Concurrency, logging and secrets hygiene

- **Decision**: Services log nothing about model text. Errors carry only the failure category enum name or fixed messages. The existing secret and endpoint scans are extended by test to cover the new project folder via their existing repository-wide scope; the Architect adds its own test with a generator that throws an exception whose message holds a fake endpoint and key, and asserts neither appears in the result, response or captured log output.
- **Rationale**: SC-004.
- **Alternatives considered**: Rely on the Requirement agent's tests: they do not exercise the new code.

## R10. Sharing model configuration between the two agents

- **Decision**: Read the model settings a second time through the same options type; do not share the existing private setup object. Both registrations derive from the same configuration keys, so they agree by construction. Add `DesignAvailability` (same shape as `DraftingAvailability`) and log one warning when not configured, with no setting name or value.
- **Rationale**: Keeps the change additive (no edit to existing lines) and the Requirement registration behaviour byte-identical.
- **Alternatives considered**: Hoisting the setup into a shared object: edits existing registration code and risks the baseline. Reusing `DraftingAvailability` for designs: couples the two readiness states without a reason.
