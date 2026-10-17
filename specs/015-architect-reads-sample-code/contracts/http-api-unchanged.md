# Contract: HTTP Behaviour Unchanged With Tools Off

Before any code change, record (first task) and keep under
`specs/015-architect-reads-sample-code/baseline/`:

1. The pass counts of the full test suite (`baseline-tests.txt`).
2. For a fixed synthetic change request and a scripted model answer: the status code, headers and
   body of the design route, the design history route and the audit listing, and the stored design
   payload.

After the change, with defaults (tools off), the same scenario must produce byte-identical
recordings, and the full existing suite must pass with no assertion edited. Existing fakes
(`FakeChatModelClient`, `FakeDesignGenerator`) must compile and behave unchanged.

With tools on, the only differences are: `sampleFilesRead` on the design draft, additional
`ToolCalled` audit entries, and the two new `Category` values on the existing 502 response. No new
route, status code, header or error code exists.
