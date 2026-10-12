# Research: Automated Change Verification

No `NEEDS CLARIFICATION` items remained after the clarification session. These are the design
decisions taken, each with the alternatives considered.

## 1. Platform

- **Decision**: GitHub Actions.
- **Rationale**: The repository is already on GitHub (`origin` is a GitHub URL, pull requests are
  in use). Hosted runners are clean per run, status is attached to the pull request, and the check
  can be made required through repository rules, which covers FR-009, FR-005 and FR-016 with no
  extra service.
- **Alternatives**: Azure Pipelines or a self-hosted runner. Rejected: extra account and
  maintenance, and a self-hosted runner would depend on a specific machine (FR-009).

## 2. Triggers

- **Decision**: `pull_request` (branches: `main`) and `push` (branches: `main`). No `paths` filter.
- **Rationale**: Covers FR-001 and FR-002. A path filter would leave documentation-only pull
  requests with no result, and a required check that never reports blocks the merge forever.
- **Alternatives**: `pull_request_target`. Rejected: it runs with the base repository's token and
  secrets against untrusted code, which breaks FR-013 and FR-014. Plain `pull_request` gives fork
  runs a read-only token and no secrets automatically.

## 3. Runtime version

- **Decision**: `actions/setup-dotnet` with `dotnet-version: 8.0.x`.
- **Rationale**: All four projects target `net8.0`. A floating patch level takes security patches
  without a file edit.
- **Alternatives**: Add `global.json` to pin an exact SDK. Rejected for now: it would also change
  every developer's local build (they have SDK 9) and is not needed to meet FR-009. Revisit if a
  patch release ever breaks a run.

## 4. Runner image

- **Decision**: `ubuntu-24.04`, named explicitly rather than `ubuntu-latest`.
- **Rationale**: A pinned image does not change under the repository when the platform moves its
  `latest` label. Linux is the cheapest and fastest hosted runner and the code has no Windows-only
  dependency.
- **Alternatives**: `windows-latest` (matches the developer machine). Rejected: slower and no
  requirement for it. A matrix of both is out of scope for this slice.

## 5. Clean state

- **Decision**: A fresh runner plus a fresh checkout; no dependency or build cache.
- **Rationale**: FR-003 asks for a build from a clean state. A package cache would speed restore but
  reintroduces state between runs. Restore time for this solution is small.
- **Alternatives**: NuGet cache keyed by lock file. Rejected for now; no lock files exist.

## 6. Time bound

- **Decision**: `timeout-minutes: 15` on the job.
- **Rationale**: Set by the clarification (15 minutes). The platform ends the job at the limit and
  the check does not report success.
- **To confirm in the quickstart**: whether a timed-out job is shown as failed or cancelled. Both
  are non-passing for a required check, so either meets FR-006.

## 7. Replacing older runs

- **Decision**: `concurrency.group` of the pull request number (or the commit SHA for pushes to
  `main`), with `cancel-in-progress` true only for pull requests.
- **Rationale**: FR-011 and FR-012. Grouping by pull request number means two different pull
  requests never cancel each other. Grouping main-line pushes by commit means a quick second merge
  does not cancel the verification of the first, so every landed change gets a verdict (User Story
  3).
- **Alternatives**: Group by branch name. Rejected: fork pull requests from branches with the same
  name (for example `main`) would collide.

## 8. Permissions and secrets

- **Decision**: Workflow-level `permissions: { contents: read }`. No `secrets` or `vars` are referenced; the only `env`
  entries are three `DOTNET_*` flags. `actions/checkout` runs with `persist-credentials: false`.
- **Rationale**: FR-013 and FR-014. Setting permissions at workflow level means a new job added
  later is also read-only. Not persisting credentials means later steps cannot push even by
  accident. The artifact upload step uses the run's own artifact service, which does not need a
  repository write permission.
- **Platform setting outside the file**: the repository's default workflow token permission should
  be read-only as well; this is documented in the quickstart, not enforced by the file.

## 9. Keeping the live model check off

- **Decision**: Nothing sets `AGENTICCHANGEOPS_LIVE_MODEL_CHECK` or any `AGENTICCHANGEOPS_MODEL_*`
  value. A guard step fails the run if any variable with the prefix `AGENTICCHANGEOPS_` or
  `AZURE_` is present.
- **Rationale**: The existing live test attribute skips unless that variable equals `1`, and the
  generator reports "not configured" without the model settings. The guard turns an accidental
  future `env:` entry into a visible failure rather than a silent model call.
- **Alternatives**: Rely on the skip alone. Rejected: it does not detect the mistake until the
  model is actually called.

## 10. Reporting a result a person can see

- **Decision**: One job with `name: verify`. The pull request shows it as a check with a pass or
  fail mark; the job summary lists the totals.
- **Rationale**: FR-005, FR-007 and FR-016. A single, short, stable name is what a repository rule
  needs to reference.
- **Note**: the name is the contract. Renaming the job silently detaches it from the required
  rule, so it is recorded in the contract file.

## 11. Evidence for diagnosis

- **Decision**: The run log keeps the full build and test console output. Test result files
  (`.trx`) are uploaded with `if: always()` and `retention-days: 30`.
- **Rationale**: FR-008 and SC-008. Console logs alone are enough to diagnose, but result files
  keep per-test detail even when the log is long, and they are what the zero-tests guard reads.
- **Alternatives**: Third-party test report actions that post annotations. Rejected: they need
  write permission on checks or pull requests, which breaks FR-013.
- **Note**: log retention is a repository setting (platform default 90 days), so the 30-day
  guarantee in SC-008 is met by both.

## 12. Zero tests discovered

- **Decision**: After `dotnet test`, a shell step sums the executed count over all `.trx` files and
  fails if no file exists or the sum is zero.
- **Rationale**: `dotnet test` succeeds when a test project discovers nothing, for example after a
  broken test adapter reference. The spec says that must not pass (FR-006, edge case).
- **Alternatives**: Assert an exact expected count. Rejected: it would need an edit with every
  new test.

## 13. Action versions

- **Decision**: Pin each third-party action to a full commit SHA, with the readable tag in a
  trailing comment. Tags are resolved and verified when the file is written (task), not guessed
  now.
- **Rationale**: A moved tag cannot change what runs inside a job that builds untrusted code.
- **Alternatives**: Major tags such as `@v4`. Rejected: mutable. Automatic updates of the pins are
  out of scope.

## 14. Fork pull requests

- **Decision**: No special handling in the file.
- **Rationale**: The platform runs fork `pull_request` workflows with a read-only token and no
  secrets. For a first-time outside contributor the platform may hold the run until a maintainer
  approves it; that is a repository setting and is noted in the quickstart.
