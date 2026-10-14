# Data Model: Architect Agent on the Agent Framework

No persisted record, enum value, serialized shape or API body changes in this slice (Principle IV; spec FR-013).

## Unchanged types

| Type | Where | Role |
|---|---|---|
| `IChatModelClient` / `ChatModelResponse(Text, WasCutOff)` | ModelAccess | The seam. Same signature and meaning. |
| `DraftGenerationException` / `GenerationFailureCategory` | ModelAccess | Failure carrier; categories unchanged. |
| `AzureOpenAIGeneratorOptions` | ModelAccess | Same settings and variable names; no new setting. |
| `DesignContent`, `DesignDraft`, `DesignDraftingResult` | Architect | Output records, untouched. |

## New type (behaviour only, no data)

| Type | Where | Role |
|---|---|---|
| `AgentFrameworkChatModelClient` | ModelAccess | `IChatModelClient` implementation that runs one stateless, tool-less agent request. Holds only the configured chat client and agent; no per-request state. |
| `ContentFilterProbe` | ModelAccess | Static helper: does a failed response body mention the content-filter marker. Extracted from the hand-written client; no data. |

## Statelessness rule

A request builds its messages from its own arguments only. No field of the new client is written after construction. A second request cannot observe the first.
