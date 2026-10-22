# Contract: Existing Behaviour Unchanged

Before any code change, record (first task) under `specs/016-developer-agent-proposals/baseline/`:

1. Pass counts of the full test suite (`baseline-tests.txt`).
2. For a fixed synthetic change request and scripted model answers: status, headers and body of the
   design route, design history, workflow status at every stage (including `DesignComplete`), advance,
   and the audit listing; plus the stored design payload.

After the change, with the Developer unused (defaults: tools off), the same scenario must produce
byte-identical recordings and the full existing suite must pass with no assertion edited. Existing
fakes compile and behave unchanged.

With the Developer available, the only differences to existing responses are: `offeredStep` on the
workflow status at `DesignComplete`, and whatever the new routes add. Automatic advancement is checked
to make no proposal, with the Developer both available and unavailable.

Expected edits to existing tests: none. If a test that enumerates every audit event type or workflow
step needs the new value, that is reported as an addition and needs approval, as in slice 015.
