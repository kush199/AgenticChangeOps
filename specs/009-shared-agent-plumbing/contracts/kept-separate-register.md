# Contract: Kept-Separate Register

Pairs of similar-looking code that are **not** shared, with the reason. The register is completed during implementation; entries below are the ones known at planning. A pair moves to shared only if a line-by-line comparison shows identical behaviour.

| # | Pair | Why it stays separate |
|---|---|---|
| K1 | `RequirementsDraftingService` / `DesignDraftingService` generator-failure translation (catch category, catch other, validate, report) | Same logic but each ends in its own result type; sharing needs a generic result type, which is close to merging the services (out of scope). Decided in clarification. |
| K2 | The two services' query methods (latest, history) | Read different store collections and return different record types; a shared version would be generic over the record and the store call, which is a service-merging step. |
| K3 | The two services' save step | Differs: the Architect rechecks the basis version and stores its requirements version; audit event differs; version counters read different collections. Overlap safety lives in each service's lock. |
| K4 | Eligibility checks | Different statuses and different extra inputs; the order of checks differs (the Architect consults the basis source only after the status passes). |
| K5 | `null` content check returning a `Generator` validation error | Four lines, but builds an intake `ValidationError`, which the neutral project may not reference. Kept per agent. |
| K6 | Generator factories (`CreateFromEnvironment`, `FromProcessEnvironment`) | Three lines each, different names used by composition and tests. Renaming one would edit callers and remove a visible difference for no benefit. |
| K7 | Prompt builders | Different prompts by rule; any change to prompts is out of scope. |
| K8 | `DraftContent` / `DesignContent`, record types, status enums | Different shapes and persisted records; stored records must not change. |
| K9 | `FakeChatModelClient` in the two test projects | Differ by the agent's sample answer; sharing needs a new test-support project. FR-019 is optional. |
| K10 | `LiveModelFactAttribute` in the two test projects | Identical, a few lines; the two live checks stay opt-in per agent. Left in place to avoid a test-support project. |

## Added during implementation (T032)

Confirmed by line-by-line comparison after the extraction; K1 to K10 above all held.

| # | Pair | Why it stays separate |
|---|---|---|
| K11 | `DraftingResult` / `DesignDraftingResult` (record plus `Success` and `Failure` factories) | One result type per agent, each carrying its own record type; a shared version is a generic result type, which the services-merging boundary rules out (see K1). About 20 shared lines remain. |
| K12 | The private `Check` helper in `DraftContentValidator` / `DesignContentValidator` (turns a shared error string into an intake validation error) | Identical eight lines, but the target type is an intake type; placing it in the neutral project would add the intake dependency that rule N2 forbids. Kept per agent beside K5. |
| K13 | Prompt builders' shared boilerplate lines (separators, section labels, JSON-shape instructions) | The text is what the model is asked to produce; the slice must not change prompts. Sharing a skeleton would couple two prompts that are expected to diverge. |

## Added by convergence (T041)

| # | Pair | Why it stays separate |
|---|---|---|
| K14 | The two agents' sets of required and optional sections (`DraftContentValidator` / `DesignContentValidator`) | The rules (trim, drop blanks, limits, wording) are shared; which sections require an item is each agent's own decision and differs on purpose: Requirement requires functional requirements and acceptance criteria; Architect requires affected components, impact and risks, and rollback considerations, and leaves dependencies optional. Each agent states its choice as `requireItem` at the call, so the difference stays visible beside the section name. Table in `dependency-rules.md`. |
| K15 | `tests/AgenticChangeOps.Api.Tests/Fakes/ScriptedChatModelClient.cs` and `tests/AgenticChangeOps.ModelAccess.Tests/Fakes/ScriptedChatModelClient.cs` | Near copies, not identical: the ModelAccess one also records the instruction, user content and output bound its output-bound test needs. Together with the two `FakeChatModelClient` copies (K9) this is four stand-ins for one seam, left separate because sharing needs a test-support project (FR-019 is optional) or links across test projects for little gain. |
