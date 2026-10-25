# Baseline recordings (T003)

The recordings contracts/http-api-unchanged.md asks for already exist and are already asserted
byte-for-byte by the existing suite, so no new recording script or scenario file was written
(same decision as slice 016):

- `tests/AgenticChangeOps.Api.Tests/Baseline/golden/transcript.txt`,
  `design-operations-transcript.txt`, `shared-plumbing-transcript.txt`,
  `caller-cancellation-transcript.txt`, `drafting-skeleton-transcript.txt`: status, headers and
  bodies of the intake, draft, design and proposal routes, histories and the audit listing, against
  both stores.
- `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`: the generated API description,
  which also fixes the route table.
- Workflow status bodies at each stage are asserted member by member in
  `tests/AgenticChangeOps.Api.Tests/WorkflowEndpointTests.cs` and the Workflow tests.

The new Reviewer-unused check (T048) therefore replays nothing new: it re-asserts these goldens with
the Reviewer registered, and adds an explicit assertion that no route, tag or schema mentions reviews.
The only golden change expected from this slice is the one additive line in
`expected-test-edits.md`.
