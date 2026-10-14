# Comparison: hand-written model call vs Microsoft Agent Framework (Architect agent)

Evidence date 2026-10-14. Framework: `Microsoft.Agents.AI` and `Microsoft.Agents.AI.OpenAI` 1.24.0.
Every claim below is backed by a test in `tests/AgenticChangeOps.ModelAccess.Tests` (named in the last table).

## What the framework replaced

Only the part of `AzureOpenAIChatModelClient` that is specific to the Architect path:

- building the chat request (system message, user message, output cap, JSON-object response format);
- sending it and reading the answer's text and finish reason.

In `AgentFrameworkChatModelClient` that is a one-shot `ChatClientAgent` given the request's system
instruction as its instructions, run once with the user content as its only message, no session,
and chat options carrying the output cap and `ChatResponseFormat.Json`.

## What it added

- Two packages (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`) and, transitively,
  `Microsoft.Extensions.AI` 10.10.x and `OpenAI` 2.14.0. About 15 new assemblies.
- A forced dependency change for the shared project: `OpenAI` 2.1.0 -> 2.14.0 is binary-incompatible
  with `Azure.AI.OpenAI` 2.1.0 (every call failed with `MissingMethodException`), so `Azure.AI.OpenAI`
  moved to **2.9.0-beta.1, a prerelease**. The Requirement agent's code is unchanged but it now runs on
  the new package set. Decided by the user ("A") on 2026-10-14; the hand-written client's wire output
  is byte-identical before and after (`golden/hand-written-wire.txt`).
- An agent object built per request (cheap; holds no state).

## What had to be kept

| Kept | Why |
|---|---|
| `ModelCaller` (time limit, size limit, blank check, category mapping, cancellation vs timeout) | The framework gives none of these; they sit above the seam and are unchanged. |
| `ModelFailureMapper` and the "no detail attached" rule | The framework surfaces SDK exceptions; they still must become a category with nothing attached. |
| `ClientRetryPolicy(0)` on the Azure client | The one-attempt guarantee comes from the SDK pipeline, not the framework; set inside the client so it cannot be forgotten. |
| Keyless sign-in (`DefaultAzureCredential`) | The framework has no opinion; unchanged. |
| `ContentFilterProbe` (extracted, now shared by both clients) | Content-filter blocks are only visible in the raw error body. |
| Walking the exception chain for the service error | The framework may wrap the SDK exception; a status is still read from the inner one. |
| `DesignResponseParser` (hand-written) | Out of scope to replace with framework structured output. |

## Behaviour that differed

| Difference | Observed | Effect |
|---|---|---|
| Output-token cap field name on the wire | `max_completion_tokens = 500` (framework) vs `max_tokens = 500` (hand-written); same value | None expected on current API versions; confirmed only by the opt-in live check (T030) |
| Prerelease `Azure.AI.OpenAI` | 2.9.0-beta.1 | Dependency risk accepted by the user; see above |

Not different (asserted equal): request path and method, header names (including bearer
`Authorization`, no `api-key`), message count, order, roles and text, response format, absence of
`tools`/`tool_choice`/`functions`, number of requests (1), every failure category.

No difference in the instructions given to the model or the structure it is asked to produce.

## Findings against the failure rules (spec FR-018, FR-019)

- Retry: none (429, 408, 500, 502, 503, 504 and a transport failure each produced exactly 1 request).
- Extra call: none.
- Retained state: none (second request carries nothing from the first; 20 parallel requests each carry only their own text).
- Configuration or model text in an error, log or console output: none.
- Failure categories: all preserved, including the finish-reason cases (Length -> cut off, ContentFilter -> malformed).

## Evidence

| Claim | Test |
|---|---|
| Same wire request except the token-cap name | `FrameworkClientWireParityTests` (+ `golden/framework-wire-diff.txt`) |
| Hand-written client unaffected by the package lift | `HandWrittenClientWireBaselineTests` (+ `golden/hand-written-wire.txt`) |
| One call, no retry, no tools | `FrameworkClientOneCallTests` |
| No state between requests | `FrameworkClientStatelessTests` |
| Categories, cancellation, nothing attached | `FrameworkClientFailureTests`, `FrameworkClientResultTests` |
| Limits through the shared caller | `FrameworkClientThroughCallerTests` |
| Nothing leaks | `FrameworkClientNoLeakTests` |
| Framework confined | `FrameworkContainmentTests` |
| HTTP surface unchanged | existing `SharedPlumbingBaselineTests`, `CallerCancellationBaselineTests` (goldens untouched) |

## Not verified by this slice

- The opt-in live check (spec FR-015, SC-003): **not run**; needs the user's own model access. It is
  also the only check of the `max_completion_tokens` name against a real service.
- The Requirement live check against the lifted package set: **not run**; the user should run it
  before merge.
