# Contract: Audit Entries And The Added Design Field

## Audit entry per tool call

Event type `ToolCalled`; `ChangeRequestId` the request; `AgentName` `ArchitectAgent`;
`OccurredAt` the call's start time; status before and after equal and unchanged; `ToolName`,
`ToolTarget`, `ToolOutcome`, `ResultBytes`, `ElapsedMilliseconds` as in data-model.md; `Details`
null. No member exists that could hold content.

Written only when the design is stored (research R-009), in call order, immediately before the
design's `DesignGenerated` entry.

JSON shape: the five new members are omitted when null, so every existing entry serializes exactly
as before.

## Design draft

`sampleFilesRead` appears in a design draft's JSON only when not null. Tools off: absent, so the
response, the stored payload and the history list are byte-identical to before. Tools on: an array
of relative paths, possibly empty.

Reading an older stored draft yields null for the member.

## Status and approval

A `ToolCalled` entry never changes status. A design draft produced with tools on has exactly the
same status effect as one without: none.
