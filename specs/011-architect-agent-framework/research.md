# Research: Architect Agent on the Agent Framework

All items resolved at plan time unless marked **verify at implementation**, which are confirmed by a red test before the code is written (Principle VI).

## R-001: Where the framework attaches

- **Decision**: Implement a second `IChatModelClient` (`AgentFrameworkChatModelClient`) in `AgenticChangeOps.ModelAccess`. Wire it in only for the Architect path.
- **Rationale**: `IChatModelClient` is the one place a model is reached. `ModelCaller` above it already owns the time limit, output-size check, cancellation vs time-out, and category mapping, so the framework inherits them unchanged. Tests that use `FakeChatModelClient` never see the swap.
- **Alternatives considered**: (a) Replace `IDesignDraftGenerator.GenerateAsync` internals with an agent that also parses: rejected, touches the parser and the seam. (b) Framework in the Architect project: rejected, would need framework packages and types in the agent project and break its "references only Intake and ModelAccess" structure test for no gain. (c) Replace `AzureOpenAIChatModelClient` for both agents: rejected, changes the Requirement path (clarification 2026-10-14).

## R-002: Packages and versions

- **Decision**: `Microsoft.Agents.AI` 1.24.0 and `Microsoft.Agents.AI.OpenAI` 1.24.0 (net8.0 targets confirmed on nuget.org). Keep `Azure.AI.OpenAI` 2.1.0 and `Azure.Identity`.
- **Rationale**: Both packages declare a net8.0 group; `Microsoft.Agents.AI.OpenAI` brings `Microsoft.Extensions.AI.OpenAI` 10.10.1 which provides the `ChatClient` → `IChatClient` bridge.
- **Alternatives considered**: `Microsoft.Extensions.AI.OpenAI` alone with a hand-rolled `IChatClient` call: rejected, that is the chat-client abstraction, not the *agent* abstraction the slice is about. Semantic Kernel agents: rejected, the constitution's "Agent Framework" is Microsoft Agent Framework and SK is deferred in earlier slices.
- **Note**: the latest stable was 1.24.0 on 2026-10-14; exact pin is re-checked in T002.

## R-003: OpenAI package lift (risk)

- **Finding**: `Microsoft.Agents.AI.OpenAI` 1.24.0 depends on `OpenAI` 2.14.0; `Azure.AI.OpenAI` 2.1.0 depends on `OpenAI` 2.1.0. NuGet will resolve to 2.14.0 for the whole project.
- **Decision**: Accept the lift; guard it. The existing hand-written client runs on the lifted package too, so the full existing suite plus a wire test on the hand-written client (baseline captured **before** the package change, compared after) shows whether the Requirement path moved.
- **Fallback**: if the hand-written client's wire output or behaviour differs after the lift, stop and record it for the user (spec FR-019); do not edit the Requirement agent.
- **verify at implementation**: build and full test run immediately after the package change, before any new class.

## R-004: Making exactly one call, no retry

- **Decision**: Build the Azure client with `RetryPolicy = new ClientRetryPolicy(0)` exactly as today, and construct the agent without any chat-client middleware (no function-invocation, no retry, no caching, no logging decorators).
- **Rationale**: The hand-written client's guarantee comes from the SDK retry policy; the framework adds no retry of its own when no middleware is configured, but that is proved by the counting transport (R-007), not assumed.
- **verify at implementation**: 429, 500, 503 and a dropped connection each produce exactly one request.

## R-005: No tools, no session, no memory

- **Decision**: Create the agent with instructions only (no tools list). Call `RunAsync` per request with a freshly built single user message and no session object passed; never hold a session field. The agent instance is stateless config; no history provider or memory provider is registered.
- **Rationale**: Spec FR-003, FR-004. Per-call construction of messages guarantees independence; absence of a stored session is checked by a test that makes two requests and compares the second wire body with a single-request body.
- **verify at implementation**: framework default session/history behaviour when no session is supplied (expected: ephemeral, discarded after the call).

## R-006: Same instructions, same structure

- **Decision**: Pass the system instruction as the agent's instructions and the user content as the sole user message; set output-token limit and JSON-object response format through the per-run chat options.
- **Rationale**: Reproduces the hand-written request: one system message, one user message, `response_format: json_object`, output-token cap.
- **Expected unavoidable differences (to be confirmed by parity test and listed in comparison.md)**: the token-cap field name on the wire (`max_completion_tokens` vs `max_tokens`) depending on the bridge; any agent/author-name attribute on messages; request headers (user agent string). Content of the messages and response format are required to be identical.
- **verify at implementation**: parity test output.

## R-007: Proving it offline

- **Decision**: A fake HTTP transport injected through the client options records every request (method, path, headers, body) and returns scripted responses or failures. Used for: wire parity between the two clients, one call and no retry, no tools field in the body, no state between requests, and failure mapping.
- **Rationale**: No live model in the suite (Principle VI) and no mocking of the framework itself, which is the thing under study. The transport is a test fake in ModelAccess.Tests; the client gets an `internal` constructor taking client options so tests can inject it (InternalsVisibleTo ModelAccess.Tests).
- **Alternatives considered**: mocking `IChatClient`: rejected, would bypass the very pipeline being compared.

## R-008: Failure categories

- **Decision**: Reuse the hand-written client's exact catch ladder in the framework client: `DraftGenerationException` passes through; `OperationCanceledException` is rethrown for `ModelCaller` to classify; `ClientResultException` maps by status and content-filter marker through `ModelFailureMapper`; anything else maps through `ModelFailureMapper.FromException`; finish reason Length → `WasCutOff`, ContentFilter → malformed output; original exception never attached.
- **Open check (verify at implementation)**: the framework may wrap SDK exceptions (for example in `InvalidOperationException` with an inner `ClientResultException`) or surface them unchanged. If wrapped, unwrap by inspecting inner exceptions for a status, without keeping any message. If a category genuinely cannot be preserved, record it and stop for the user (FR-019).
- **Shared helper**: the "response body mentions content_filter" probe is extracted to `ContentFilterProbe` because it now has two callers (Principle VII).

## R-009: Finish reason and text from the framework response

- **Decision**: Read text from the agent response's text and the finish reason from the underlying chat response (`ChatFinishReason.Length` → cut off; `ContentFilter` → malformed output).
- **verify at implementation**: the exact member that exposes the finish reason on the agent response; if it is not exposed, use the response's raw representation. If neither works, record and stop (FR-019): without a finish reason the "cut off" category cannot be preserved.

## R-010: Constitution amendment

- **Decision**: PATCH amendment 1.0.3 → 1.0.4 to the Technology Stack note: the framework is Microsoft Agent Framework (not Semantic Kernel), used to run the Architect agent's single content-generation call as a stateless, tool-less agent behind `IChatModelClient`; routing and decisions stay deterministic C# (Principle II); introduced to study and compare framework behaviour on one agent. Raised as its own `docs:` PR, merged before the slice PR, as in slices 007 and 008.
- **Rationale**: Wording refinement of an existing binding choice; no principle is added or redefined.

## R-011: Telemetry and logging

- **Decision**: None added. No logger or telemetry decorator is registered on the framework agent, so nothing from the framework can log request or response text. A test asserts that a failing call writes nothing to a captured logger and exposes no detail through exception messages.
- **Rationale**: Out of scope (telemetry exporters) and FR-008.
