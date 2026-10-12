# Contract: Verification Workflow

The stable surface other things depend on. Changing any item here is a breaking change to the
merge rule and must be called out in the pull request.

## Identity

| Item | Value |
|------|-------|
| File | `.github/workflows/ci.yml` |
| Workflow name | `CI` |
| Job id and name | `verify` |
| Check name to require | `verify` |

The check name is what a branch rule references. Renaming the job leaves the rule waiting for a
check that never arrives, which blocks every merge.

## Triggers

| Event | Filter | Result reported on |
|-------|--------|--------------------|
| `pull_request` | target branch `main` (opened, updated, reopened) | The pull request |
| `push` | branch `main` | The landed commit |

No path filters. No schedule. No manual trigger and no `pull_request_target`.

## Permissions

```yaml
permissions:
  contents: read
```

Nothing else, at workflow level. No `secrets.*`, `vars.*` or model-setting values. The only `env` entries are the workflow-level flags `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`.

## Bounds

| Setting | Value |
|---------|-------|
| `timeout-minutes` | 15 |
| `concurrency.group` | pull request number, or commit SHA for pushes to `main` |
| `cancel-in-progress` | true for pull requests, false for pushes to `main` |

## Steps (order is part of the contract)

1. Checkout, without persisted credentials.
2. Install .NET SDK `8.0.x`.
3. Guard: fail if any `AGENTICCHANGEOPS_*` or `AZURE_*` variable is set.
4. Restore the solution.
5. Build the solution in Release configuration.
6. Test the solution, no rebuild, writing `.trx` result files and printing console output.
7. Guard: fail if no result file exists or no test executed.
8. Upload result files as artifact `test-results`, always, retained 30 days.

## Outputs

| Output | Where | Kept |
|--------|-------|------|
| Pass or fail mark | Check `verify` on the pull request or commit | Permanent |
| Full console output | Run log | Platform setting (default 90 days) |
| Test result files | Artifact `test-results` | 30 days |
| Totals | Job summary | With the run |

## Not provided (out of scope)

Deployment, package publishing, coverage thresholds, live model checks, scheduled runs,
notifications, and any write to the repository.
