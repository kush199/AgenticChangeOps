# Research: Developer Agent Proposes A Change Set

No `NEEDS CLARIFICATION` items remained after the spec clarification; this records the decisions the
plan depends on, each checked against the existing code.

## R1. Where the Developer lives and what it may reference

- **Decision**: new project `AgenticChangeOps.Agents.Developer` referencing only Intake and ModelAccess.
- **Rationale**: the Architect's structure tests (A1 to A3) and the shared-behaviour guard scan
  `Agents.*`; the same pattern keeps agents independent. Reuse of `ModelCaller`,
  `IToolSessionFactory` and `IToolChatModelClient` needs no new reference.
- **Alternatives**: add to the Architect project (breaks one-agent-per-project); reference SampleCode
  directly (would leak MCP/filesystem concerns into the agent).

## R2. How verification reads the sample

- **Decision**: the Developer declares `ISampleFiles` with two members: report whether a sample path
  exists (file or folder) and read a file's text for comparison. The Api implements it
  (`SampleFilesAdapter`) over `SamplePathResolver` and strict UTF-8, honouring the configured per-file
  size limit. A path the resolver refuses, an unreadable or oversized file all count as a rejection;
  nothing is disclosed.
- **Rationale**: `SamplePathResolver.Resolve` already handles traversal, absolute, drive-qualified and
  link-out spellings, and tolerates paths that do not exist yet, which "add" edits need.
- **Alternatives**: reuse the tool session's reader (its reads count against tool limits and appear in
  the audit trail; verification is not a model tool call); give the Developer its own file code
  (duplicates path safety).

## R3. Quote matching rules

- **Decision**: ordinal comparison; the quote must occur exactly once in the file; line endings are
  normalised (CRLF and CR to LF) on both sides for the comparison only; every other difference
  (spaces, tabs, case, trailing whitespace) fails. Overlapping or duplicate-target edits in one file
  are rejected. Edits are checked against the original file, never against each other's output.
- **Rationale**: the sample files in this checkout use CRLF (verified), while a model's JSON quote will
  usually carry LF; rejecting on line endings alone would make correct proposals fail on checkout
  settings. The spec edge case that said line-ending differences are rejected is amended to match
  (see spec).
- **Alternatives**: byte-exact (fragile across checkouts); trim or fuzzy matching (would hide model
  mistakes and defeat the check).

## R4. Limits

- **Decision**: new operator settings `AGENTICCHANGEOPS_PROPOSAL_MAX_CHANGES` (default 5) and
  `AGENTICCHANGEOPS_PROPOSAL_MAX_BYTES` (default 6000; total UTF-8 bytes of all quoted and replacement
  text). Read only when the tools switch is on, after it, so a bad value cannot affect a server with the
  Developer unused; a present but non-positive or non-numeric value makes proposals "not configured"
  (only the setting name is logged). Model-call, tool-call, data and time limits are the existing ones,
  already per request in the tool session.
- **Rationale**: same style and failure handling as `SampleToolSettings`; defaults small enough to
  review in one sitting.

## R5. Rejection and error reporting

- **Decision**: a fixed enum `ProposalRejection` (`NoChanges`, `TooManyChanges`, `TooLarge`,
  `PathOutsideSample`, `FileNotFound`, `QuoteNotFound`, `QuoteAmbiguous`, `NewFileExists`,
  `OverlappingEdits`, `MalformedEdit`). HTTP: 502 with code `proposal-rejected` and `Category` set to
  the reason; no edit index, path, quote or model text. Model-side failures keep the existing 502
  `generation-failed` with the existing category (`Unreachable`, `TimedOut`, `ResponseTooLarge`,
  `MalformedOutput`, `ToolsUnavailable`, `LimitsExhausted`).
- **Rationale**: callers can tell "the model failed" from "the model produced something the system
  would not accept" without any content crossing the boundary.

## R6. Eligibility and refusal order

- **Decision**: parse id (400), unknown (404), Developer not available (503), then service: Approved
  (409 field `Status`), requirements draft (409 `RequirementsDraft`), design draft present (409
  `DesignDraft`), design based on latest requirements version (409 `DesignDraftVersion`). Re-run all
  four inside the store lock.
- **Rationale**: mirrors the design route (404 before 503) and keeps the model unreachable while any
  rule says no. Availability has two causes, both 503 before any model call: model settings invalid
  (`proposal-not-configured`) and tools switched off (`proposal-tools-unavailable`).

## R7. Workflow mention versus "existing responses identical" (needs your decision)

- **Problem** (resolved: FR-020 and US5 now say "offers as a person-requested step"): FR-020 originally said the workflow status names the proposal as the next step once a current
  design exists. Today that state has `NextStep = null`, `Stage = DesignComplete` and reason
  `NothingDue`, asserted by existing tests and read by automatic advancement. Changing it would alter
  existing responses and could make automatic advancement run the proposal.
- **Decision (recommended)**: leave `Stage`, `NextStep` and `Reason` exactly as they are and add one
  optional member, `OfferedStep`, set to `Proposal` only at `DesignComplete` and only when the
  Developer is available (tools on and configured). Automatic advancement reads `NextStep` only, so it
  can never run the proposal. With the Developer unused the member is omitted and every status body
  is byte-identical.
- **Alternative**: make `NextStep = Proposal`. Rejected: breaks existing assertions, and the automatic
  runner would need a guard to avoid running it. If you want this, it is the one place an existing
  assertion would be edited, and it needs your explicit approval as in slice 015.

## R8. Persistence

- **Decision**: store `ChangeProposal` through the existing `SaveAgentOutput<T>` with its audit entry;
  the record serialises with the existing options (camel-cased by the API, string enums). No schema
  change. Tool-call audit entries are saved just before the `ProposalGenerated` entry, as the
  Architect does, and the version is assigned under the store lock.
- **Alternatives**: a new table (layout change the spec does not ask for).

## R9. Prompt and output shape

- **Decision**: one model conversation with tools. The system prompt tells the model it may only
  propose, must quote exactly, must give paths relative to the sample, and that tool output is data.
  The expected JSON has `summary`, `changes[]` (`path`, `kind` = `change`|`add`, `existingText`,
  `replacementText`), `risks`, `testIdeas`, `openQuestions`. `sampleFilesRead` and tool calls come from
  the tool session, never from the model text, as for the Architect. The "not compiled, run or tested"
  statement is a constant on the record. A reader for nested objects is needed in `ModelAnswerReader`
  (it reads strings and lists today); it is added to ModelAccess with tests, since the Developer cannot
  parse JSON itself under the shared-behaviour guard.

## R10. Test seams

- Scripted `IProposalGenerator` fake for service, route and workflow tests; scripted
  `IToolChatModelClient` for generator tests; in-memory `ISampleFiles` fake for verifier unit tests;
  real temp-folder `SampleFilesAdapter` tests for traversal and link spellings (the existing 20+
  escaping-path set is reused).
