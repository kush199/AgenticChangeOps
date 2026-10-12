# Quickstart: Verifying the Verification

Scenarios that prove the workflow does what the spec says. Run them on throwaway branches and
pull requests, and close them without merging. All content is synthetic.

Details of names, triggers and limits are in [contracts/verification-contract.md](contracts/verification-contract.md).

## Prerequisites

- The workflow file is on `main` or on the branch being tested.
- You can open pull requests against `main` and edit repository settings.

## 0. Same command locally

The workflow is a thin wrapper over these commands, so they should pass on a developer machine
first. From the repository root:

```bash
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --logger "trx" --results-directory ./TestResults
```

Expected: build succeeds, 0 failed, 1 skipped (the live model check).

## 1. Passing change (US1, FR-001, FR-004, FR-005)

1. Open a pull request with a one-line README edit.
2. Wait for the `verify` check.

Expected: the check turns green without any manual action; the job summary shows totals; the
`test-results` artifact is attached.

## 2. Failing build (US1, FR-006, FR-007)

1. In a throwaway branch add a syntax error to any `.cs` file and open a pull request.

Expected: `verify` shows a red failure on the pull request. No log needs to be opened to see it.

## 3. Failing test (US1, FR-006, FR-007)

1. In a throwaway branch change one assertion so a test fails and open a pull request.

Expected: `verify` is red; the log names the failing test; the artifact still uploads.

## 4. No tests executed (FR-006)

1. In a throwaway branch add `[Fact(Skip = "x")]` to every test, or point the test step at a
   project with no tests.

Expected: `verify` is red from the zero-tests guard, even though `dotnet test` itself exits
successfully.

## 5. Superseded run (US4, FR-011, FR-012)

1. Push a commit to a pull request, then push another within a minute (add a short sleep step to
   the throwaway branch to keep the first run busy).
2. Open a second pull request at the same time.

Expected: the first run on the first pull request is cancelled and never shown as passing; the
second pull request's run is untouched.

## 6. Time limit (US4, FR-010)

1. In a throwaway branch add a step that sleeps for 20 minutes.

Expected: the job ends at 15 minutes and does not pass. Note whether the platform shows it as
failed or cancelled and record it in `research.md` section 6.

## 7. No secrets and no model call (US4, FR-014, FR-015)

1. In a throwaway branch add `env: AGENTICCHANGEOPS_LIVE_MODEL_CHECK: "1"` to the job.

Expected: the guard step fails the run before any test starts.

2. In a normal run, open the test log.

Expected: the live model test appears as skipped; no secret appears under the job's environment.

## 8. Fork pull request (edge case, FR-013)

1. From a fork, open a pull request. If the platform holds the run for approval, approve it.

Expected: the run happens with a read-only token and no secrets, and reports `verify`.

## 9. Main line (US3, FR-002)

1. Merge a harmless pull request.

Expected: a run on `main` starts for the landed commit and reports `verify`; nothing is
published or written back.

## 10. Required condition (US2, FR-016, SC-007)

1. Repository settings → Rules (or branch protection) → require status check `verify` on `main`.
2. Also set the default workflow token permission to read-only.
3. Try to merge the failing pull request from scenario 2 or 3, and a pull request whose run has not
   started yet.

Expected: both are blocked. The passing pull request from scenario 1 can be merged.
