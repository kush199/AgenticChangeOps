# Implementation Plan: Architect Consults A Sample Codebase

**Branch**: `feature/architect-reads-sample-code` | **Date**: 2026-10-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-architect-reads-sample-code/spec.md`

## Summary

Give the Architect's single model request three read-only tools (list files, read file, search text) over a small synthetic sample codebase, served by an in-process Model Context Protocol server, and record the files it read on the stored design draft. Everything is off unless an operator switches it on.

- **Sample codebase**: new top-level folder `sample-codebase/` (two invented systems), outside `src/`, outside the solution, never compiled.
- **New project `AgenticChangeOps.SampleCode`**: the deterministic part: path confinement, the three tools, per-request limits and counters, call records, and an in-process MCP server/client pair joined by in-memory pipes (no process, no network, no port). It exposes the tools to the model as ordinary function tools.
- **`AgenticChangeOps.ModelAccess`**: a second implementation of the model seam that runs the Architect's request on the Microsoft Agent Framework *with* tools and a model-call cap (`AgentFrameworkToolChatModelClient`), plus agent-neutral records (`ToolCallRecord`, `ToolSession`) and two new failure categories.
- **Architect project**: the generator opens a tool session per request when tools are on, appends one fixed paragraph to the system text, and returns the files read and call records inside `DesignContent` (new trailing optional members). `DesignDraft` gains one nullable member, `SampleFilesRead`. The Architect project still references only Intake and ModelAccess.
- **Intake**: `AuditEventType` gains `ToolCalled` (appended) and `ChangeRequestAuditEntry` gains optional typed members for a call. All additive; absent members are not serialized, so tools-off output is byte-identical.
- **Api**: reads the operator settings once, builds the session factory only when switched on, ships the sample folder next to the binaries. Two new failure categories flow through the existing `generation-failed` (502) mapping with `Category` set; no route, status or body shape changes.

Why this holds the guarantees: routing, eligibility, approval and storage rules are untouched; the model only returns content (Principle II); tool output is placed in the model's tool-result channel and never in the system text, so it cannot change instructions or the tool set (the tool list is fixed per request before any content is read); every limit is enforced by our code, not by the model.

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), unchanged

**Primary Dependencies**: adds `ModelContextProtocol` (2.2.0, ships a net8.0 target, brings `ModelContextProtocol.Core`) to the new `AgenticChangeOps.SampleCode` project only. Existing `Microsoft.Agents.AI` and `Microsoft.Extensions.AI` in ModelAccess provide the tool-calling loop. See research R-001, R-002.

**Storage**: Existing SQLite file and in-memory store. No layout change: agent outputs and audit entries are JSON payloads, so the added members are additive JSON members and the layout version stays at 2. The sample folder is read-only files.

**Testing**: xUnit; scripted fake model client and a fake `IChatClient` that issues tool calls; real temporary sample folders for confinement tests (including link and traversal cases); no live model in the suite. Live check stays opt-in and skipped when unconfigured.

**Target Platform**: Unchanged (CI on ubuntu runner, local Windows). Path tests must pass on both separators.

**Project Type**: Multi-project .NET solution; one new project (`AgenticChangeOps.SampleCode`) plus its test project.

**Performance Goals**: N/A. The request time limit bounds total work.

**Constraints**:
- Tools off: zero change to any status code, body, header, error code or stored byte; all existing tests pass with assertions unchanged (existing fakes and signatures compile as they are).
- Read-only: the tool set contains exactly list, read, search; none accepts anything that writes, and the process never opens a file for writing under the sample.
- Confinement: every path is resolved against the sample root, with links resolved, and refused if the result is not inside it.
- No retry of any failed call. No content, model text, endpoint, deployment name, credential or outside path in any error, log, response or audit entry.
- Framework and MCP types stay out of the Architect project, Api endpoints, Intake and Workflow.

**Scale/Scope**: 1 new source project, 1 new test project, ~10 new source files, ~3 edited files per existing project (Intake 2, ModelAccess 3, Architect 4, Api 3, Persistence 0), 1 sample folder of 12 small files, constitution amendment (separate PR), README section.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Reviewed against constitution v1.0.4.

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. The whole sample is invented, with a test that scans it for emails, URLs with real hosts, key-like strings and the names the project forbids. Test sample folders use the same fictional names. |
| II. Deterministic routing | Pass. The model still only generates content. The tool set is fixed in code; tool results cannot add tools or change instructions; no routing, eligibility or transition code is touched. Limits are enforced by deterministic code. |
| III. Human approval gates | Pass. Reading is classified non-consequential in the spec (FR-010) and the plan records it in the contract; nothing is committed, triggered or deployed; drafting still changes no status and approves nothing. |
| IV. Typed auditable records | Pass with additive changes. `DesignDraft.SampleFilesRead` (nullable), `DesignContent` trailing optional members, `AuditEventType.ToolCalled` (appended) and optional typed audit members are non-breaking under the constitution's additive rule and are listed in Complexity Tracking. Tool calls are typed records, not strings. |
| V. Spec-driven | Pass. Spec clarified; this plan precedes tasks; no code before tasks are approved. |
| VI. Test-first | Pass. Confinement, limits, audit and failure tests are written and approved red first. The model stays behind `IChatModelClient`-family seams; the suite uses fakes only. |
| VII. Simplicity | **Flagged, justified.** One new project and a second client seam have one production caller (Architect) where the rule asks for two. The new project is a distinct deterministic concern (path safety and limits) that must be reviewable and testable alone, and the Technology Stack already names MCP as the standard tool protocol. No fifth agent is added. Recorded in Complexity Tracking. |

Technology stack: the Agent Framework note says "tool-less" and "tools ... needs its own slice and spec". This slice is that slice for tools on the Architect agent only. The note and the MCP line need a PATCH amendment (1.0.4 → 1.0.5), raised as its own constitution PR before this slice's PR merges, following the 007, 008 and 011 precedent. **Gate status: PASS (with the Principle VII note) before research and after design.**

## Project Structure

### Documentation (this feature)

```text
specs/015-architect-reads-sample-code/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── tools.md                 # the three tools: inputs, outputs, refusal messages, classification
│   ├── settings-and-failures.md # operator settings, defaults, the two new failure categories
│   ├── audit-and-record.md      # the call audit entry and the one added design-draft member
│   └── http-api-unchanged.md    # what is recorded before and compared after, tools off
├── checklists/requirements.md
└── tasks.md                     # produced by /speckit-tasks
```

### Source Code (repository root)

```text
sample-codebase/                          # NEW, not in the solution, never compiled
├── README.md                             # says it is invented and read-only
├── orders-service/{config,src,notes}/...
└── billing-portal/{config,src,notes}/...

src/
├── AgenticChangeOps.SampleCode/          # NEW project (references ModelAccess; MCP package)
│   ├── SampleToolSettings.cs             # parse/validate operator settings, safe defaults
│   ├── SamplePathResolver.cs             # confinement (traversal, absolute, drive, link)
│   ├── SampleReader.cs                   # list / read / search, pure and read-only
│   ├── ToolBudget.cs                     # per-request counters and ceilings
│   ├── SampleToolSession.cs              # one request: budget + recorder + in-process MCP pair
│   ├── SampleToolSessionFactory.cs       # implements ModelAccess's factory seam
│   └── SampleMcpServer.cs                # registers the three tools on an MCP server
├── AgenticChangeOps.ModelAccess/
│   ├── ToolCallRecord.cs                 # NEW: agent-neutral record of one call
│   ├── ToolSet.cs                        # NEW: framework-free tool definitions + invoke delegate (no `object`)
│   ├── IToolSession.cs                   # NEW: tools + records + files read, opened per request
│   ├── IToolSessionFactory.cs            # NEW: opens a session or throws ToolsUnavailable
│   ├── IToolChatModelClient.cs           # NEW: seam for the tool-capable model call (fakeable)
│   ├── AgentFrameworkChatModelClient.cs  # + AgentFrameworkToolChatModelClient (same file: the only one allowed to name framework types)
│   ├── GenerationFailureCategory.cs      # + ToolsUnavailable, LimitsExhausted (appended)
│   └── DraftGenerationException.cs       # + two fixed messages
├── AgenticChangeOps.Intake/
│   └── Models/{ChangeRequestAuditEntry.cs, Enums/AuditEventType.cs}   # additive
├── AgenticChangeOps.Agents.Architect/
│   ├── DesignContent.cs, DesignDraft.cs  # additive trailing members
│   ├── AzureOpenAIDesignGenerator.cs     # tool path when a session factory is supplied
│   ├── DesignPromptBuilder.cs            # tools-on instruction text (tools-off text unchanged)
│   └── DesignDraftingService.cs          # stores files list and call audit entries
└── AgenticChangeOps.Api/
    ├── Composition/ApiServiceRegistration.cs   # reads settings, wires factory only when on
    └── AgenticChangeOps.Api.csproj             # copies sample-codebase/ next to the binaries

tests/
├── AgenticChangeOps.SampleCode.Tests/    # NEW: confinement, limits, tools, MCP round trip, sample hygiene
├── AgenticChangeOps.ModelAccess.Tests/   # + tool-loop, model-call cap, failure mapping, no leak
├── AgenticChangeOps.Agents.Architect.Tests/  # + files list, audit entries, tools off identical
└── AgenticChangeOps.Api.Tests/           # + tools-off baseline unchanged; tools-on failure responses
```

**Structure Decision**: One new source project and one new test project; the sample folder sits at the repository root beside `specs/`. Dependency rules: `SampleCode` → `ModelAccess` (and the MCP package); `Architect` → `Intake`, `ModelAccess` only (unchanged); `Api` → everything. Nothing references `SampleCode` except `Api` and its tests.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New project and second client seam with one production caller (Principle VII) | Path confinement and limits are safety-critical deterministic code that must be testable and reviewable alone; MCP is the stack's tool protocol | Putting it in ModelAccess would mix a filesystem reader into the model-access project and break its agent-neutral, no-I/O character; putting it in the Architect project would let framework/MCP types leak into it |
| Additive members on persisted/public records (Principle IV): `DesignDraft.SampleFilesRead`, `DesignContent` trailing members, `AuditEventType.ToolCalled`, optional typed members on `ChangeRequestAuditEntry` | The spec requires one added stored field and per-call audit entries with typed fields | Details strings would make audit untyped; a new table would be a layout change the spec rules out. All are null by default and omitted when null, so no existing serialized value changes |
| `ToolCalled` audit entries carry the argument and only the size and outcome of the output (Principle IV `InputPayload`/`OutputPayload` by reference) | Keeps file content out of the audit trail (FR-018) | Storing the output would put sample content into audit and storage, which the spec forbids |
| Two new `GenerationFailureCategory` values (appended) | Spec requires two distinct failure categories | Reusing `Unreachable` or `MalformedOutput` would make the failures indistinguishable |

## Constitution compliance check (pre-code)

Recorded 2026-10-17 at T001, after the user approved spec.md, plan.md and tasks.md. Reviewed against constitution v1.0.4.

| Principle | Result |
|---|---|
| I. Synthetic data only | Pass. The sample and every fixture are invented; a hygiene test scans the sample (T029). |
| II. Deterministic routing | Pass. The model only generates content; the tool set and limits are fixed in code; tool output cannot add tools or change instructions. |
| III. Human approval gates | Pass. Reading sample files is classified non-consequential (spec FR-010, contracts/tools.md); no commit, trigger or deploy occurs; drafting changes no status. |
| IV. Typed auditable records | Pass. Only additive nullable members; tool calls are typed `ToolCalled` entries holding the argument and the size and outcome of the output by reference; the tool set is a typed `ToolSet`, never `object`. |
| V. Spec-driven | Pass. Spec, plan and tasks approved by the user before any code. |
| VI. Test-first | Pass. Tests written red and approved at T032 before behaviour; model access behind `IChatModelClient` and `IToolChatModelClient`; fakes only. |
| VII. Simplicity | Pass with note. One new project and seam have one production caller; recorded in Complexity Tracking. |
| Technology Stack | The v1.0.5 amendment (Agent Framework tools for the Architect, MCP in use, sample folder) is drafted in `constitution-amendment-draft.md` and is opened as its own PR (T058). |
