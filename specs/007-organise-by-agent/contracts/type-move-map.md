# Contract: Type Move Map

Renamed or removed types: **none**. Namespace and project changes are moves, not renames (spec assumption). One public store contract changes; it is listed below.

## Moved types

| Type | From | To |
|---|---|---|
| `RequirementsDraft` | `AgenticChangeOps.Intake.Models` | `AgenticChangeOps.Agents.Requirement` |
| `DraftContent` | `AgenticChangeOps.Intake.Models` | `AgenticChangeOps.Agents.Requirement` |
| `DraftingResult` | `AgenticChangeOps.Intake.Models` | `AgenticChangeOps.Agents.Requirement` |
| `DraftStatus` | `AgenticChangeOps.Intake.Models.Enums` | `AgenticChangeOps.Agents.Requirement` |
| `DraftContentValidator` | `AgenticChangeOps.Intake.Services` | `AgenticChangeOps.Agents.Requirement` |
| `IRequirementsDraftGenerator` | `AgenticChangeOps.Intake.Services` | `AgenticChangeOps.Agents.Requirement` |
| `IRequirementsDraftingService` | `AgenticChangeOps.Intake.Services` | `AgenticChangeOps.Agents.Requirement` |
| `RequirementsDraftingService` | `AgenticChangeOps.Intake.Services` | `AgenticChangeOps.Agents.Requirement` |
| `AzureOpenAIDraftGenerator` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.Agents.Requirement` |
| `DraftPromptBuilder` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.Agents.Requirement` |
| `DraftResponseParser` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.Agents.Requirement` |
| `IChatModelClient`, `ChatModelResponse` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.ModelAccess` |
| `AzureOpenAIChatModelClient` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.ModelAccess` |
| `AzureOpenAIGeneratorOptions` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.ModelAccess` |
| `GeneratorConfigurationException` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.ModelAccess` |
| `ModelFailureMapper` | `AgenticChangeOps.Intake.AzureOpenAI` | `AgenticChangeOps.ModelAccess` |
| `DraftGenerationException` | `AgenticChangeOps.Intake.Models` | `AgenticChangeOps.ModelAccess` |
| `GenerationFailureCategory` | `AgenticChangeOps.Intake.Models.Enums` | `AgenticChangeOps.ModelAccess` |

## Unmoved but changed (listed, per FR-010)

| Item | Change | Reason |
|---|---|---|
| `IChangeRequestStore.SaveDraft(RequirementsDraft, ChangeRequestAuditEntry)` | Replaced by `SaveAgentOutput<T>(T, ChangeRequestAuditEntry)` | Intake must not name the draft type (FR-004, Q1). |
| `IChangeRequestStore.GetDrafts(Guid)` | Replaced by `GetAgentOutputs<T>(Guid)` | Same. |
| `SaveDraft` / `GetDrafts` as seen by callers | Provided as extension methods in the agent namespace, same signatures | Keeps call sites and assertions unchanged (FR-007). |
| `RequirementsDraft` | Implements `IAgentOutput` | Additive; no member or serialized change. |

## Kept as-is on purpose

- `AuditEventType.DraftGenerated`, `ChangeRequestAuditEntry.AgentName`, `ChangeRequestAuditEntry.DraftVersion`: existing audit vocabulary, names and values preserved (FR-012).
- `DraftGenerationException` keeps its name although it now lives in the neutral project; see research R5. Candidate rename when a second agent is added.
- `AzureOpenAIGeneratorOptions` / `GeneratorConfigurationException`: names kept; `Generator` here means "model-backed generation", not the requirement agent.
