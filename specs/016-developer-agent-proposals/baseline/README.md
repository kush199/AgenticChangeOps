# Baseline recordings (T004)

The recordings contracts/http-api-unchanged.md asks for already exist and are already asserted
byte-for-byte by the existing suite, so no new recording script was written:

- `tests/AgenticChangeOps.Api.Tests/Baseline/golden/transcript.txt`,
  `design-operations-transcript.txt`, `shared-plumbing-transcript.txt`,
  `caller-cancellation-transcript.txt`: status, headers and bodies of the design routes, design
  history and audit listing, against both stores.
- `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`: the generated API description.
- Workflow status bodies at each stage (including `DesignComplete`) are asserted member by member in
  `tests/AgenticChangeOps.Api.Tests/WorkflowEndpointTests.cs` and the Workflow tests; they are not in a
  golden transcript. T023 therefore adds an explicit check that the `DesignComplete` status body has
  exactly its current members when the Developer is unused.

Known golden changes from this slice (additions only, see expected-test-edits.md): `openapi.json`
(new `AuditEventType` value, the three new operations, the new schemas).
