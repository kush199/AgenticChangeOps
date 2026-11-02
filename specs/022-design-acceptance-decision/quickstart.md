# Quickstart: Design Acceptance Decision

Prerequisites: .NET 8 SDK; run from the repo root. No model or credentials are needed; tests use the
in-memory store and the existing fake design generator.

1. Whole suite: `dotnet test`. Expected: all pre-existing tests pass; only assertions that enumerate
   operations, audit event types or the published description changed.
2. Service rules: `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter DesignDecision`.
   Expected: accept and reject recorded; a second decision, an older version, a design on stale
   requirements, an unknown version, a non-Approved request and bad fields are all refused with nothing
   stored; overlapping decisions store exactly one.
3. Endpoint and retrieval: `dotnet test tests/AgenticChangeOps.Api.Tests --filter DesignDecision`.
   Expected: 201 with Location; error codes per [contracts/http-api.md](contracts/http-api.md); latest and
   history show `decision` only for decided versions; decision-less JSON equals the pre-slice JSON.
4. Storage parity: the shared store suite runs save, read and duplicate for `DesignDecision` against both
   the in-memory and SQLite stores.
5. Isolation: a structure test confirms Workflow and the advancement host never reference the service; a
   decision leaves workflow status, change request status and proposals unchanged.
6. Optional manual check (local, synthetic data): start the API, submit and approve a synthetic change
   request, request a design (needs design drafting configured), then
   `POST /change-requests/{id}/designs/1/decisions` with
   `{"approverName":"Sample Approver","decision":"Accept"}` and
   `GET /change-requests/{id}/designs/latest`. Not required for the slice.

Fields and shapes: see [data-model.md](data-model.md).
