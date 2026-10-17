# Data Model: Architect Consults A Sample Codebase

All additions are nullable or optional and omitted from serialized output when null. The layout
version of durable storage is unchanged (payloads are JSON).

## Changed records

### DesignDraft (Architect project)

| Member | Type | Notes |
|---|---|---|
| `SampleFilesRead` | `IReadOnlyList<string>?` (new, last, default null) | Null: tools were off, or the draft predates this feature. Empty: tools on, nothing read. Otherwise relative paths, de-duplicated, first-read order |

### DesignContent (Architect project)

| Member | Type | Notes |
|---|---|---|
| `SampleFilesRead` | `IReadOnlyList<string>?` (new, optional, default null) | Carried from the generator to the service |
| `ToolCalls` | `IReadOnlyList<ToolCallRecord>?` (new, optional, default null) | Carried to the service for auditing; not stored on the draft |

### ChangeRequestAuditEntry (Intake)

New optional members, all null for every existing event:

| Member | Type | Notes |
|---|---|---|
| `ToolName` | `string?` | `list_files`, `read_file` or `search_text` |
| `ToolTarget` | `string?` | Relative path, or truncated and cleaned search term |
| `ToolOutcome` | `string?` | `Served` or a refusal outcome name |
| `ResultBytes` | `int?` | Size returned; 0 when refused |
| `ElapsedMilliseconds` | `int?` | Time taken |

### AuditEventType (Intake)

`ToolCalled` appended after `DesignGenerated`.

### GenerationFailureCategory (ModelAccess)

`ToolsUnavailable` and `LimitsExhausted` appended.

## New records (ModelAccess, agent-neutral)

### ToolCallRecord

`ToolName`, `Target`, `Outcome`, `ResultBytes`, `Elapsed`. No content member exists.

### ToolCallOutcome (enum)

`Served`, `RefusedOutsideSample`, `RefusedNotFound`, `RefusedTooLarge`, `RefusedNotText`,
`RefusedCallLimit`, `RefusedDataLimit`, `RefusedTimeLimit`, `RefusedInvalidArgument`.

### IToolSession and IToolSessionFactory

A session is opened per design request and disposed after it. It exposes the model-facing tools as
a typed `ToolSet` (a sealed ModelAccess class holding framework-free `ToolDefinition`s and one
invoke delegate; the tool client turns them into framework functions; it is never `object`), the call records so far, and the files read. The factory throws
`DraftGenerationException(ToolsUnavailable)` when it cannot open one.

### IToolChatModelClient

The seam for a tool-capable model call, in ModelAccess:
`CompleteAsync(system, user, maxOutputTokens, IToolSession session, ct)` returning
`ChatModelResponse`. `AgentFrameworkToolChatModelClient` implements it; tests use a fake.

### Naming

"Files relied on" in the spec is the field `SampleFilesRead` (JSON `sampleFilesRead`).

## New in the SampleCode project

### SampleToolSettings

Switch, max tool calls, max model calls, max data returned, max file bytes, time limit, optional
sample folder. Validation follows `AzureOpenAIGeneratorOptions`: setting name only in errors,
never values.

### ToolBudget

Counters for calls used and bytes returned. Admits a call or returns the refusal outcome.
Thread-safe; one per session.

## Lifecycle

Design request, tools on: session opened → model loop with tool calls → session yields
`SampleFilesRead` and `ToolCallRecord`s → service validates the content → under the store lock:
one `ToolCalled` entry per record, then the design and its `DesignGenerated` entry → session
disposed. On any failure the session is disposed and nothing is written.

## Validation rules

- Every `SampleFilesRead` entry is relative, uses `/`, is non-empty, and corresponds to a served
  `read_file` record in the same request.
- Audit `ToolTarget` is at most 120 characters with control characters removed.
- No member anywhere carries file content.
