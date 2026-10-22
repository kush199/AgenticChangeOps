# Contract: Kept-Separate Register (final, slice 017)

Pairs of similar-looking code that are **not** shared, with the reason. Every entry of slice 009's register (K1 to K15) was re-examined line by line on the final code. A pair moves to shared only if behaviour is identical.

## Slice 009 entries, re-examined

| # | Pair | Final status | Reason (updated) |
|---|---|---|---|
| K1 | Generator-failure translation in the three services | **Resolved** | Identical in all three; now in `DraftingEngine.RunAsync`. The one difference (the Developer rethrows cancellation) is kept visible as N1. |
| K2 | Latest and history reads | **Resolved** | Identical apart from the store collection, which each agent supplies as `ReadAll`; now `DraftingEngine.GetLatest` and `GetHistory`. |
| K3 | Save step | **Partly resolved** | The locked step (re-check, currency hook, clock, version, save call) is in `DraftingEngine`. Building the record and the audit entries stays per agent: different members, event types and marker handling (N3). Files: the `Save` method in each of the three services. |
| K4 | Eligibility checks | **Kept, reason updated** | Different statuses, inputs, order and messages (Requirement: Submitted, Deferred, or Approved with no draft; Architect: Approved with a requirements draft; Developer: Approved with requirements and a design based on them, basis source consulted only after the status passes). The engine only fixes when eligibility is called (before generation and again in the locked step). Files: each nested `Agent.CheckEligibility`. |
| K5 | Null-content check returning a `Generator` validation error | **Kept** | Builds an intake `ValidationError` from agent content; each agent's validator keeps its own. Files: `DraftContentValidator`, `DesignContentValidator`, `ProposalContentValidator`. |
| K6 | Generator factories (`CreateFromEnvironment`, `FromProcessEnvironment`, ...) | **Kept** | Different names used by composition and tests. |
| K7 | Prompt builders | **Kept** | Prompt text is what the model is asked to produce; changing prompts is out of scope. |
| K8 | Content, record and status types | **Kept** | Different shapes and persisted records; stored records must not change. |
| K9 | `FakeChatModelClient` in test projects | **Kept** | Differ by sample answer; sharing needs a test-support project. |
| K10 | `LiveModelFactAttribute` in test projects | **Kept** | Identical few lines, opt-in live checks stay per agent. |
| K11 | Result types (`DraftingResult`, `DesignDraftingResult`, `ProposalResult`) | **Kept, reason updated** | Public types used by the API, Workflow and the unchanged tests. A shared generic result would change those types. Each service maps the engine's neutral outcome to its result in one expression. Files: the three result types and the mapping in each service's operation. |
| K12 | The private `Check` helper in content validators | **Kept** | Turns a shared error string into an intake validation error, per agent. |
| K13 | Prompt builders' shared boilerplate lines | **Kept** | Prompts are out of scope. |
| K14 | Required and optional sections per agent | **Kept** | Intentional difference, visible beside each section name. |
| K15 | `ScriptedChatModelClient` copies in test projects | **Kept** | Near copies, not identical; sharing needs a test-support project. |

## New entries from this slice

| # | Pair | Final status | Reason |
|---|---|---|---|
| N1 | Cancellation handling: the Developer rethrows when the caller's own token is cancelled; Requirement and Architect report a generator failure naming the exception type | **Kept, visible** | Intentional difference with different callers' behaviour. A flag `PropagatesCancellation` on each agent's `Agent` class states it beside a comment; the engine honours it. Proven: engine tests in both directions, plus one characterization test per agent written on the old code. |
| N2 | Tool-call audit entry mapping in the Architect and Developer services | **Resolved** | Line-by-line diff: the two copies differed only in one comment word and the agent-name constant. Now `ToolCallAuditEntries.Create(agentName, ...)`. It builds an audit entry, an Intake-level shape every agent stores, so FR-005 allows it (reading agreed at convergence); the Requirement agent simply has no tool calls. The loop over a content's tool calls stays per agent: the Architect tolerates a null list, the Developer does not. |
| N3 | Automatic marker (accepted by Requirement and Architect, not by the Developer) | **Kept** | Carried by each service's per-call save delegate; the engine never sees it. |
| N4 | The Developer's quote verification and its rejection result | **Kept** | Its own rule and result type; runs inside the Developer's `Review`. |
| N5 | The currency checks: none (Requirement), requirements version (Architect), requirements and design versions (Developer) | **Kept** | Different inputs and messages. Files: each `Agent.CheckStillCurrent`. |
| N6 | `HasDraft` lock in the Requirement agent's eligibility | **Kept** | A read of the store under its own lock to decide whether an Approved request may still be drafted; no other agent has this rule. It is the only remaining `SyncRoot` use in the agents, and the sequence scan (rule D6) does not forbid it. |
| N7 | Repeated nested-class boilerplate in the three `Agent` classes (constructor assigning collaborators, `CountExisting`, `ReadAll`) | **Kept** | Each reads a different store collection and holds different collaborators. A shared base class would hide the differences (research R3). |
