# Quickstart: Validating Durable Storage

Prerequisites: .NET 8 SDK, network access for the first `dotnet restore` (new package). All data is synthetic.

## 1. Automated checks

```powershell
dotnet test AgenticChangeOps.sln
```

Expected: every pre-existing test passes with unchanged assertions; the new contract suite passes
for both stores; durable-only tests (restart, atomicity, layout, unavailable, no-leak, concurrency) pass.

## 2. Restart survives (manual)

```powershell
$env:ChangeOps__Storage__Path = "$env:TEMP\changeops-demo.db"
dotnet run --project src/AgenticChangeOps.Api
# In another shell: POST /change-requests with a synthetic body, approve it, note the id.
# Stop the API (Ctrl+C), start it again with the same setting.
# GET /change-requests/{id} and /audit return the same values, status and order.
```

## 3. Unavailable storage

Set `ChangeOps__Storage__Path` to a path inside a non-existent read-only location and start the API.
It starts; `POST /change-requests` returns 503 `storage-unavailable`; no path appears in the body or log.

## 4. Unsupported layout

Create a SQLite file with `PRAGMA user_version = 99`, point the setting at it, call any operation:
503 `storage-unavailable` with category `UnsupportedLayout`; the file is byte-identical afterwards.

## 5. Repository hygiene

`git status` shows no `.db` file after running the API with defaults (the default path is outside the repository).

See [data-model.md](data-model.md), [contracts/store-contract.md](contracts/store-contract.md) and
[contracts/error-responses.md](contracts/error-responses.md).

## Verified (2026-10-16, Windows, Debug build, synthetic data)

- **Restart (step 2)**: submitted and approved one request against a temp database, stopped the API, started it again with the same setting. `GET /change-requests/{id}` and `/audit` were byte-identical before and after.
- **Unavailable (step 3)**: with the database path under an ordinary file, the API started, `GET /openapi/v1.json` answered 200, and `POST /change-requests` answered `503 storage-unavailable` with category `Unavailable`. The log line was `Request refused: POST /change-requests -> 503 storage-unavailable.`; the log held no path and no `sqlite` text.
- **Repository hygiene (step 5)**: `git status` showed no database file after the runs.
- Step 4 (unsupported layout) is covered by `LayoutVersionTests` and `StorageErrorTests.UnsupportedLayout_...` rather than by hand.
- Not exercised: a real process kill in the middle of a write. Atomicity is covered by the injected-failure tests (`AtomicityTests`), and SQLite's transactions give the on-crash guarantee.
