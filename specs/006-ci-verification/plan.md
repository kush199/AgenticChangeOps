# Implementation Plan: Automated Change Verification

**Branch**: `006-ci-verification` (working branch: `feature/ci-pipeline`) | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-ci-verification/spec.md`

## Summary

Add one GitHub Actions workflow file that builds the whole solution from a fresh checkout and runs
the full test suite on every pull request to `main` and every push to `main`. It publishes one
stable check named `verify`, so a maintainer can make it a required condition. No application code
and no new .NET project changes.

The workflow is the whole feature, so the design is mostly a list of fixed settings:

- **Triggers**: `pull_request` targeting `main`, and `push` to `main`. No path filters, so
  documentation-only changes still produce a result.
- **Environment**: a hosted Linux runner, a pinned runner image, .NET SDK 8.0.x (the projects
  target `net8.0`).
- **Bounds**: job timeout of 15 minutes; a concurrency group per pull request with
  cancel-in-progress, so a newer run replaces an older one.
- **Safety**: workflow-level `contents: read` and nothing else; no `secrets` or `vars`, no model setting, and only three `DOTNET_*` flags as `env`; a
  guard step that fails the run if any model setting or the live-check opt-in is present.
- **Evidence**: console output with normal verbosity, plus the test result files uploaded as a
  run artifact kept for 30 days, even when the run fails.
- **Honest pass**: a guard step fails the run when zero tests executed.

## Technical Context

**Language/Version**: C# / .NET 8 (all four projects declare `net8.0`). CI installs SDK `8.0.x`.
The developer machine has SDK 9.0.101; that is not used by CI.

**Primary Dependencies**: GitHub Actions only: `actions/checkout`, `actions/setup-dotnet`,
`actions/upload-artifact`. Each is pinned to a full commit SHA with the release tag in a comment.

**Storage**: N/A. Run output is held by the platform (run log plus one artifact).

**Testing**: The existing xUnit suite (`Intake.Tests` 291 tests with 1 skipped, `Api.Tests` 97 at
the time of planning). The workflow itself is verified by scenarios in
[quickstart.md](quickstart.md) run on a throwaway pull request, because a workflow file has no
unit-test surface.

**Target Platform**: GitHub-hosted `ubuntu-24.04` runner. The suite already runs on Windows locally,
so the Linux run is also a first check of path and case sensitivity.

**Project Type**: Repository automation (no source project)

**Performance Goals**: Typical run under 10 minutes (SC-004); hard limit 15 minutes (FR-010).

**Constraints**: Read-only token; no secrets; no network call to a model; nothing published,
deployed or written back; works for fork pull requests with the same restrictions.

**Scale/Scope**: One workflow file, one job, about six steps. One documentation addition to the
README.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design. All pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Synthetic Data Only | ✅ Pass | No secret, endpoint or deployment name is referenced. The workflow holds no `secrets.*` or `vars.*` reference at all. |
| II. Deterministic Routing | ✅ Pass | No routing or agent logic is touched. |
| III. Human Approval Gates | ✅ Pass | The workflow takes no consequential action: it reads, builds, tests and reports. It approves nothing and merges nothing. It is therefore non-consequential per FR-013 and FR-017, and that classification is recorded in spec.md Assumptions for the compliance check. |
| IV. Typed Auditable Records | ✅ Pass | No persisted record changes. |
| V. Spec-Driven Slice by Slice | ✅ Pass | Spec and clarification are done; tasks follow and need approval before the workflow is written. |
| VI. Test-First | ⚠️ Pass with notes | The model stays behind `IChatModelClient` and the suite never calls a model. A workflow has no unit tests; its checks are the failing-then-passing scenarios in the quickstart, which will be agreed and approved before the workflow is written. |
| VII. Simplicity | ✅ Pass | No agent, no project, no abstraction. One file, one job. |

**Technology stack note**: the constitution names .NET 8 or later and xUnit; both are used
unchanged. It does not name a CI platform; GitHub Actions is chosen because the repository is
already hosted on GitHub. No amendment is needed.

**Gate result: PASS, checked against constitution v1.0.1.**

## Project Structure

### Documentation (this feature)

```text
specs/006-ci-verification/
├── plan.md                         # This file
├── research.md                     # Phase 0
├── data-model.md                   # Phase 1
├── quickstart.md                   # Phase 1
├── contracts/
│   └── verification-contract.md    # Phase 1: triggers, check name, permissions, outputs
├── checklists/
│   └── requirements.md
└── tasks.md                        # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── ci.yml          # new: the verification workflow (the only new code file)
README.md               # edit: short "Automated verification" section and status badge
```

No change to `src/`, `tests/` or the solution file.

**Structure Decision**: a single workflow file at the conventional location. Splitting build and
test into separate jobs was rejected: it doubles the clean checkout and restore time for no
gain, and one job gives one clear check name.

## Complexity Tracking

> No constitution violations to justify.

| Item | Why | Note |
|------|-----|------|
| Guard step for model settings | A future edit could add an `env:` value by mistake; the spec requires that none ever reaches a run (FR-014, FR-015) | About five lines of shell; remove if it ever grows |
| Zero-tests guard | `dotnet test` exits successfully when no test is discovered; the spec says that must fail (FR-006) | Reads the result files already produced |
