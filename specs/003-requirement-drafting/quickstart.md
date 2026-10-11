# Quickstart: Validating Requirement Drafting

Proves the slice works end to end using a fake generator. No model, network, or credentials.

## Prerequisites

- .NET 8 SDK
- Repository checked out on the feature branch

## Run the tests

```powershell
dotnet test AgenticChangeOps.sln
```

Expected: all slice 001, 002, and 003 tests pass; no test reaches the network.

## Scenarios to confirm

Each maps to a test class; see [spec.md](spec.md) for the scenarios and
[contracts/IRequirementsDraftingService.md](contracts/IRequirementsDraftingService.md) for
signatures.

| # | Scenario | Expected | Test class |
|---|----------|----------|------------|
| 1 | Submit a synthetic request, then `DraftAsync` with a fake returning complete content | Version 1, status `Draft`, five sections, one `DraftGenerated` audit entry, request status still `Submitted` | `DraftingTests` |
| 2 | Draft the same request twice more | Versions 2 and 3; versions 1 and 2 unchanged | `DraftingTests` |
| 3 | Fetch latest and history | Version 3; history 1, 2, 3 | `DraftRetrievalTests` |
| 4 | Draft an unknown, `Approved`, and `Rejected` request | Distinct errors; fake generator call count 0 | `DraftEligibilityTests` |
| 5 | Fake throws / returns null / omits a section / returns empty requirements | Specific error each time; no draft, audit entry, or version consumed | `DraftGenerationFailureTests`, `DraftContentValidationTests` |
| 6 | Submit a request and never ask for a draft | No drafts; generator call count 0 | `DraftIsolationTests` |
| 7 | Generated text says "approved"; then check clearance | Status unchanged; `IsClearedToProceed` false | `DraftIsolationTests` |

## Secret check

```powershell
git grep -inE "api[_-]?key|openai\.azure|endpoint=|bearer " -- src tests
```

Expected: no hits that are real credentials or endpoints (SC-008). Code and tests only; the
spec documents quote the pattern and would match themselves.
