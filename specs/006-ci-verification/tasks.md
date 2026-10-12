# Tasks: Automated Change Verification

**Input**: Design documents from `specs/006-ci-verification/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅ | quickstart.md ✅

**Tests**: Included, in the only form this slice allows. A workflow file has no unit-test
surface, so the "tests" are the numbered scenarios in [quickstart.md](quickstart.md), run on
throwaway pull requests. Constitution Principle VI still applies: within each story the scenario
tasks come first, record the expected red state (no `verify` check, or the wrong verdict), and
end with a human-approval CHECKPOINT before the workflow is written or changed for that story. One
batched approval across stories is acceptable if the user gives it. No scenario may call a real
model or use a real credential.

**Organization**: Tasks are grouped by user story. The slice adds one new file,
`.github/workflows/ci.yml`, and one README section. Nothing under `src/` or `tests/` changes.

**Outward-facing steps**: tasks marked **[USER]** push branches, open or close pull requests, or
change repository settings. Claude drafts the commands and waits for the user to confirm or run
them. Throwaway branches are named `feature/ci-pipeline-scn-<scenario>`, are based on `feature/ci-pipeline`,
target `main`, are never merged, and are deleted afterwards.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks). Tasks
  that edit the same file are never marked `[P]`.
- **[Story]**: Which user story this task belongs to (US1 to US4)

---

## Phase 1: Setup

**Purpose**: Confirm the baseline and gather what the workflow needs.

- [X] T001 Run `dotnet restore`, `dotnet build --no-restore -c Release` and `dotnet test --no-build -c Release --logger trx --results-directory ./TestResults` from the repository root; confirm 0 failed and record the counts as the baseline (expected at planning time: Intake.Tests 291 total with 1 skipped, Api.Tests 97)
- [X] T002 [P] Resolve full commit SHAs for the three actions with `git ls-remote https://github.com/actions/checkout "refs/tags/v4*"`, the same for `actions/setup-dotnet` and `actions/upload-artifact`; choose the newest `v4.x.y` of each that is at least two weeks old, use the dereferenced (`^{}`) commit when the tag is annotated, and write the three `owner/repo@<sha> # v4.x.y` strings into a scratch note for T008
- [X] T003 [P] Check that a workflow linter is available (`actionlint --version`); if not, note that T012 will fall back to a YAML parse check only

**Checkpoint**: Local build and tests are green and the pins are known.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The skeleton every story builds on: identity, triggers, least-privilege token.
Nothing here builds or tests yet.

- [X] T004 Create `.github/workflows/ci.yml` containing only: workflow `name: CI`; `on` with `pull_request` (`branches: [main]`) and `push` (`branches: [main]`) and no `paths`, `schedule` or `workflow_dispatch`; top-level `permissions:` with only `contents: read`; one job with id `verify`, `name: verify`, `runs-on: ubuntu-24.04`, and a single placeholder step `run: echo "verification pending"`. Per [contracts/verification-contract.md](contracts/verification-contract.md): no `secrets.*` or `vars.*` reference and no model setting anywhere (the only `env` entries are the three `DOTNET_*` flags added in T005)
- [X] T005 Add top-level `env` with only `DOTNET_NOLOGO: "true"`, `DOTNET_CLI_TELEMETRY_OPTOUT: "true"` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE: "true"` in `.github/workflows/ci.yml` (these are not settings of the model and carry no secret; the guard in T024 rejects any `AGENTICCHANGEOPS_*` or `AZURE_*` variable, so these three do not trip it)

**Checkpoint**: The file parses, and the contract's identity, triggers and permissions sections are satisfied.

---

## Phase 3: User Story 1 - Know whether a proposed change is safe to merge (Priority: P1) 🎯 MVP

**Goal**: Every proposed change gets a clean build, the full test suite and a visible pass or fail,
with the full output kept.

**Independent Test**: Scenarios 1 to 4 in the quickstart: a passing change is green; a build
break, a failing test and a zero-test run are each red without opening a log.

### Tests for User Story 1 (scenarios first)

- [ ] T006 [US1] Prepare the four scenario branches as local commits on top of `feature/ci-pipeline` without pushing: `feature/ci-pipeline-scn-pass` (one-line README edit), `feature/ci-pipeline-scn-build-break` (a syntax error in `src/AgenticChangeOps.Intake/` any `.cs` file), `feature/ci-pipeline-scn-test-fail` (one assertion flipped in `tests/AgenticChangeOps.Intake.Tests/`), `feature/ci-pipeline-scn-no-tests` (every `[Fact]` in `tests/AgenticChangeOps.Api.Tests/` changed to `[Fact(Skip = "synthetic")]` and the same in the Intake tests)
- [X] T007 [US1] Record the expected red state of quickstart scenarios 1 to 4 against the placeholder workflow from T004: the `verify` check appears but is green for all four, which is wrong for scenarios 2, 3 and 4 (a green on a broken change is the failure the slice exists to remove); show this table to the user

**CHECKPOINT**: User replies "approved" to the scenario list and expected states before T008.

### Implementation for User Story 1

- [X] T008 [US1] Replace the placeholder step in `.github/workflows/ci.yml` with, in order: `actions/checkout` (pinned SHA from T002) with `persist-credentials: false`; `actions/setup-dotnet` (pinned SHA) with `dotnet-version: 8.0.x`; a step running `dotnet --version` that fails unless the output starts with `8.` (FR-009); `dotnet restore AgenticChangeOps.sln`; `dotnet build AgenticChangeOps.sln --no-restore -c Release`; `dotnet test AgenticChangeOps.sln --no-build -c Release --logger "trx" --logger "console;verbosity=normal" --results-directory ./TestResults`
- [X] T009 [US1] Add a step after the test step in `.github/workflows/ci.yml`, named `Require executed tests`, that fails when the sum of the `executed` attribute over every `<Counters>` element in `TestResults/**/*.trx` is zero or no `.trx` file exists, for example `files=$(find TestResults -name '*.trx'); test -n "$files" || { echo "no test result files"; exit 1; }; n=$(grep -ho 'executed="[0-9]*"' $files | grep -o '[0-9]*' | awk '{s+=$1} END {print s+0}'); echo "executed tests: $n"; test "$n" -gt 0`; give it `if: success()` semantics only (the default)
- [X] T010 [US1] Add a final step in `.github/workflows/ci.yml` using `actions/upload-artifact` (pinned SHA) with `if: always()`, `name: test-results`, `path: TestResults/**/*.trx`, `retention-days: 30`, `if-no-files-found: warn`
- [X] T011 [US1] Add a step before the artifact upload in `.github/workflows/ci.yml` with `if: always()` that appends one line with the executed count (or "no result files") to `$GITHUB_STEP_SUMMARY`, so the totals appear on the run page

### Verification for User Story 1

- [X] T012 [US1] Lint `.github/workflows/ci.yml` with `actionlint` (or parse it with `python -c "import yaml,sys; yaml.safe_load(open(sys.argv[1]))" .github/workflows/ci.yml`); run `grep -nE "secrets\.|vars\." .github/workflows/ci.yml` and confirm no match
- [ ] T013 [US1] **[USER]** Push the four branches from T006 after rebasing them on the new workflow, open one pull request per branch against `main`, and confirm quickstart scenarios 1 to 4: scenario 1 green with totals and a `test-results` artifact; scenarios 2, 3 and 4 red on the pull request page; scenario 3's log names the failing test and still uploads the artifact; scenario 4 red from the zero-test guard; the log's `dotnet --version` line shows 8.x. Record each run's duration (SC-004) and how long after the push the check showed its verdict (SC-002), then the four run links in a note for the user, then close the pull requests unmerged and delete the branches

**Checkpoint**: User Story 1 is complete and demonstrable on its own (MVP).

---

## Phase 4: User Story 2 - Block merging of unverified or failing changes (Priority: P2)

**Goal**: The result is a stable, documented check that a maintainer can make a required
condition.

**Independent Test**: Quickstart scenario 10.

### Tests for User Story 2

- [ ] T014 [US2] Record the expected red state of scenario 10 before any setting is changed: a pull request with a red `verify` check can still be merged, and a pull request with no run yet can be merged; show the user the settings to be changed (require status check `verify` on `main`; default workflow token permission read-only)

**CHECKPOINT**: User replies "approved" before T015.

### Implementation for User Story 2

- [ ] T015 [US2] Add an "Automated verification" section to `README.md` after "Repository layout": what runs and when, the check name `verify`, where to find the log and the `test-results` artifact, that no secret or model setting is available to a run and the live model check stays skipped, and the two settings a maintainer must apply (require `verify` on `main`; read-only default token). Use no real names; link to [contracts/verification-contract.md](contracts/verification-contract.md)
- [ ] T016 [US2] Add a status badge line for the `CI` workflow at the top of `README.md` using the repository's `actions/workflows/ci.yml/badge.svg` URL on branch `main`

### Verification for User Story 2

- [ ] T017 [US2] **[USER]** Apply the two repository settings from T014 and repeat quickstart scenario 10: a red `verify` blocks the merge, a pull request without a run blocks the merge, and a green one can merge. Report back what the settings page showed

**Checkpoint**: User Stories 1 and 2 both work.

---

## Phase 5: User Story 3 - Confirm the main line itself stays healthy (Priority: P3)

**Goal**: A change landing on `main` is verified too.

**Independent Test**: Quickstart scenario 9.

### Tests for User Story 3

- [ ] T018 [US3] Record the expected red state of scenario 9: before the workflow is on `main`, merging a harmless pull request produces no run on `main`. Note that the workflow file reaches `main` only when this slice's pull request merges, so the green confirmation happens on the next merge after that

### Implementation for User Story 3

No file change: the `push` trigger on `main` was written in T004. The slice is complete when the
trigger is shown to work, in T019.

### Verification for User Story 3

- [ ] T019 [US3] **[USER]** After this slice's pull request merges, check the Actions tab: a `CI` run for the merge commit on `main` exists and reports `verify`; then after the next harmless merge, repeat for that commit. Confirm no run published, deployed or committed anything (the run's job list shows only the `verify` job and the repository has no new commit from it)

**Checkpoint**: User Stories 1 to 3 work.

---

## Phase 6: User Story 4 - Safe, bounded, and economical runs (Priority: P3)

**Goal**: Hung runs end, superseded runs are cancelled, and no model setting can reach a run.

**Independent Test**: Quickstart scenarios 5, 6, 7 and 8.

### Tests for User Story 4 (scenarios first)

- [ ] T020 [US4] Prepare local branches `feature/ci-pipeline-scn-supersede` (adds a step `run: sleep 120` before the build in a temporary copy of the workflow, plus a second commit with a trivial README change), `feature/ci-pipeline-scn-hang` (adds `run: sleep 1200`), and `feature/ci-pipeline-scn-env` (adds `env: { AGENTICCHANGEOPS_LIVE_MODEL_CHECK: "1" }` on the job), none pushed
- [ ] T021 [US4] Record the expected red state against the workflow as it is after T011: the superseded run is not cancelled and both finish; the sleeping run keeps going until the platform's default six-hour limit; the run with the live-check variable passes the guard-less pipeline and the live test is attempted (it would fail on missing settings, but the point is that nothing stops it earlier); show the table to the user

**CHECKPOINT**: User replies "approved" before T022.

### Implementation for User Story 4

- [ ] T022 [US4] Add `timeout-minutes: 15` to job `verify` in `.github/workflows/ci.yml`
- [ ] T023 [US4] Add top-level `concurrency` to `.github/workflows/ci.yml`: `group: ci-${{ github.event.pull_request.number || github.sha }}` and `cancel-in-progress: ${{ github.event_name == 'pull_request' }}`
- [ ] T024 [US4] Insert a step named `Refuse model settings` directly after setup-dotnet in `.github/workflows/ci.yml` that fails when `env | grep -E '^(AGENTICCHANGEOPS_|AZURE_)'` matches, printing only the matching variable names and never their values

### Verification for User Story 4

- [ ] T025 [US4] **[USER]** Rebase the three branches from T020 onto the final workflow, push them, and confirm quickstart scenarios 5, 6 and 7: the first run of `feature/ci-pipeline-scn-supersede` ends cancelled and is never shown as passing; a second concurrent pull request is untouched; `feature/ci-pipeline-scn-hang` ends at 15 minutes and does not pass (record whether the platform shows failed or cancelled in research.md section 6; either is acceptable because neither passes a required check); the time from the second push to the first run showing cancelled is under 1 minute (SC-005); `feature/ci-pipeline-scn-env` fails at `Refuse model settings`; in a normal run the log shows the live model test skipped. Close all pull requests unmerged and delete the branches
- [ ] T026 [US4] **[USER]** Quickstart scenario 8: if a second account is available, open a pull request from a fork and confirm the run happens (after approval if the platform asks) with no secrets and reports `verify`; if no second account is available, say so and leave this scenario open rather than marking it done

**Checkpoint**: All four user stories work.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T027 Re-read `.github/workflows/ci.yml` against [contracts/verification-contract.md](contracts/verification-contract.md) line by line: identity, triggers, permissions, bounds, step order, outputs; fix the file or the contract so they agree
- [ ] T028 [P] Confirm every functional requirement FR-001 to FR-018 maps to at least one task or scenario; list any gap in the pull request description
- [ ] T029 [P] Confirm all content added by this slice is synthetic and carries no real name, endpoint, deployment name or key (`grep -rniE "key|secret|endpoint" .github README.md specs/006-ci-verification`, then read each hit)
- [ ] T030 Constitution v1.0.1 compliance check on spec, plan, tasks and the pull request; state in the pull request that the workflow is non-consequential under Principle III
- [ ] T031 Run `dotnet test AgenticChangeOps.sln` locally once more and confirm the T001 baseline is unchanged; `git status` shows only `.github/workflows/ci.yml`, `README.md` and `specs/006-ci-verification/`

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US1 (MVP). US2, US3 and US4 each depend only on Phase 2 and on the final shape of the workflow file, but all edit the same file, so they are done in order, not in parallel.
- US2's README tasks (T015, T016) do not touch the workflow and may be done while US1 or US4 verification waits on the platform.
- T017 needs a workflow on the pull request's base, so it runs after US1 is verified.
- T019 needs this slice's pull request merged.

## Parallel Opportunities

- T002 and T003 (independent lookups).
- T028 and T029 (read-only reviews).

## Implementation Strategy

1. Phase 1 and Phase 2, then US1 only: that alone gives a visible pass or fail on every change.
2. Stop and validate US1 on throwaway pull requests (T013) before adding anything else.
3. Add US4's guards next because they protect the secret-free promise, then US2's documentation and required setting, and confirm US3 after the merge.

## Notes

- Total tasks: 31. Per story: US1 8 (T006 to T013), US2 4 (T014 to T017), US3 2 (T018, T019), US4 7 (T020 to T026), Setup 3, Foundational 2, Polish 5.
- A `[USER]` task is not complete until the user reports what they saw; Claude does not assume a green run.
