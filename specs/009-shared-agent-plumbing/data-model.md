# Data Model: Shared Agent Plumbing

No persisted record, enum value, audit event or serialized shape changes. This lists the in-memory types the slice adds to the agent-neutral project and the agent-owned types they serve. All new types are in namespace `AgenticChangeOps.ModelAccess`.

## New shared types

### ModelCaller

| Member | Description |
|---|---|
| `ModelCaller(AzureOpenAIGeneratorOptions options, IChatModelClient client)` | Rejects null arguments exactly as the generators do today. |
| `Task<string> GetAnswerTextAsync(string systemInstruction, string userContent, CancellationToken cancellationToken)` | Makes exactly one call with `ceil(MaxResponseCharacters / 3)` as the output bound; no retry. Returns the answer text. |

Failure behaviour (unchanged, in this order of evaluation):

| Situation | Result |
|---|---|
| Client throws `DraftGenerationException` | Rethrown unchanged |
| Caller's token cancelled | `OperationCanceledException` propagates |
| Own time limit expired | `DraftGenerationException(TimedOut)` |
| Any other exception | `DraftGenerationException(ModelFailureMapper.FromException(ex))`, original not attached |
| Answer was cut off, or longer than `MaxResponseCharacters` | `DraftGenerationException(ResponseTooLarge)` |
| Answer empty or whitespace | `DraftGenerationException(MalformedOutput)` |

### ModelAnswerReader and ModelAnswer

| Member | Description |
|---|---|
| `static T ModelAnswerReader.Read<T>(string text, Func<ModelAnswer, T> map)` | Unwraps a bare object or exactly one fenced block (unlabelled or `json`, any case), parses it, requires an object, runs `map`. |
| `ModelAnswer.ReadString(string name)` | The named property if present and a string; otherwise malformed. |
| `ModelAnswer.ReadList(string name)` | The named property if present and an array of strings; otherwise malformed. |

Any failure inside `Read`, including a missing key or an exception thrown by `map`, becomes `DraftGenerationException(MalformedOutput)` with no detail; an existing `DraftGenerationException` passes through. Present-but-empty values are passed through unchanged.

### SectionNormaliser and SectionOutcome<T>

| Member | Description |
|---|---|
| `const int MaxTextLength = 2000`, `const int MaxListItems = 50` | The limits used today. |
| `static SectionOutcome<string> Text(string? value, string section)` | Trim; error if empty (`"{section} is required and must not be blank."`) or longer than the limit (`"{section} must not exceed 2000 characters."`). |
| `static SectionOutcome<IReadOnlyList<string>> List(IReadOnlyList<string>? items, string section, bool requireItem)` | Missing → `"{section} section is missing."`; drop blank, trim; none left and required → `"{section} must contain at least one non-blank item."`; too many → `"{section} must not exceed 50 items."`; any too long → `"Each {section} item must not exceed 2000 characters."`. |
| `record SectionOutcome<T>(T? Value, string? Error)` | Exactly one of the two is set. |

## Agent-owned types (unchanged)

| Agent | Kept in the agent | Uses from shared |
|---|---|---|
| Requirement | `DraftContent`, `RequirementsDraft`, `DraftingResult`, `DraftStatus`, `DraftPromptBuilder`, `DraftResponseParser` (section names), `DraftContentValidator` (required choices: functional, acceptance), `RequirementsDraftingService`, eligibility, storage, audit | `ModelCaller`, `ModelAnswerReader`, `SectionNormaliser` |
| Architect | `DesignContent`, `DesignDraft`, `DesignDraftingResult`, `DesignDraftStatus`, `DesignPromptBuilder`, `DesignResponseParser` (section names), `DesignContentValidator` (required choices: components, impact, rollback), `DesignDraftingService`, eligibility, basis check, storage, audit | `ModelCaller`, `ModelAnswerReader`, `SectionNormaliser` |

## Stand-in third agent (tests only)

A throwaway content type with its own section names and its own required choice, written in `ModelAccess.Tests`, using all three shared types and no production edit. It exists to prove FR-011 and is not shipped.
