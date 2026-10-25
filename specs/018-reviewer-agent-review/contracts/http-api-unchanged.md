# Contract: Existing Behaviour Unchanged

No HTTP operation is added or changed in this slice.

Before any code change, record (first task) under `specs/018-reviewer-agent-review/baseline/`:

1. Pass counts of the full test suite (`baseline-tests.txt`).
2. For a fixed synthetic change request and scripted model answers: status, headers and body of the
   draft, design and proposal routes and their histories, the workflow status at every stage, advance,
   and the audit listing; plus the stored payloads.

After the change, with the Reviewer unused, the same scenario must produce byte-identical recordings
and the full existing suite must pass with no assertion edited. Existing fakes compile and behave
unchanged. Specifically: the route table is identical; the OpenAPI document is identical; the workflow
status shape is identical; the automatic path makes no review.

Expected edits to existing tests: none. If a test that enumerates every audit event type needs the
new value, that is reported as an addition and needs approval, as in slices 015 and 016. The Drafting
structure tests scan every `Agents.*` folder and apply to the Reviewer unchanged.
