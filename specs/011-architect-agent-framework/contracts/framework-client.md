# Contract: AgentFrameworkChatModelClient

Implements `IChatModelClient.CompleteAsync(systemInstruction, userContent, maxOutputTokens, cancellationToken)`.

## Request (must match the hand-written client)

| Aspect | Requirement |
|---|---|
| Messages | One system message with `systemInstruction` verbatim, one user message with `userContent` verbatim, in that order. No other message. |
| Response format | JSON object |
| Output cap | `maxOutputTokens` |
| Tools | None offered |
| Retries | None (SDK retry policy 0, no framework middleware) |
| Calls | Exactly one per `CompleteAsync` |
| Sign-in | Operator's cloud identity (keyless); no key variable |
| State | None kept between calls |

## Response

| Condition | Result |
|---|---|
| Normal completion | `ChatModelResponse(text, WasCutOff: false)` |
| Finish reason Length | `ChatModelResponse(text, WasCutOff: true)` |
| Finish reason ContentFilter | `DraftGenerationException(MalformedOutput)` |
| No text | `ChatModelResponse("", ...)` (caller maps blank to MalformedOutput) |

## Failures (category only, nothing attached)

| Source | Category |
|---|---|
| `DraftGenerationException` already raised | Unchanged |
| Cancellation | Rethrown as cancellation for `ModelCaller` to classify |
| Service error with content-filter marker | MalformedOutput |
| Service error status 408 | TimedOut |
| Other service error, identity or network failure | Unreachable |
| `TimeoutException` | TimedOut |

No exception message, model text, endpoint, deployment name or credential is attached to or logged by any failure.
