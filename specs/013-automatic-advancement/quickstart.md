# Quickstart: Validating Automatic Advancement

Prerequisites: .NET 8 SDK. All data below is synthetic. Run from the repository root.

## 1. Automated checks (no model, no network)

```text
dotnet build AgenticChangeOps.sln
dotnet test AgenticChangeOps.sln
```

Expected: every pre-existing test still passes unchanged (SC-009) apart from the listed layout-version
test edits, and the new tests pass. They use fake generators and a fake clock, so no timer, model or
network is involved. The new tests cover: the due-step table, the per-check step limit, the waiting time,
the hand-over after repeated failures, overlap with a manual advance, overlapping automatic attempts,
restart, off by default, settings fallback, the automatic marker, the status additions, the operator
summary, and no leakage of a deliberately leaky fake generator.

## 2. Off by default

Start the API with no automatic-advancement settings. Submit a synthetic change request, wait longer than
any interval.

Expected: no draft appears; the workflow status of the request shows `"automatic": { "mode": "Off" }`;
`GET /operator/automatic-advancement` returns `{ "enabled": false, "lastCheck": null }`.

## 3. Switch on (needs a configured model for a real draft)

Set `ChangeOps__AutoAdvance__Enabled=true` and `ChangeOps__AutoAdvance__IntervalSeconds=10`, with the
model settings from the existing slices. Start the API, submit a synthetic request.

Expected within about one interval plus the model's time: the request has a requirements draft; its audit
history shows the draft entry with `"automatic": true`; the status shows `"mode": "On"`; the operator
summary shows `examined >= 1`, `ran >= 1`. Approve the request through the existing approval operation:
a design draft follows on a later check. A request awaiting a decision, Rejected, or Deferred with a draft
gains nothing.

*Live model run: not part of the automated suite; run it by hand if wanted.*

## 4. Model not configured

Switch on without model settings.

Expected: no step runs, no failure is recorded (`failed` stays 0, no row is written), status shows `On`.

## 5. Failure, wait and hand-over (automated)

With a fake generator that always fails with a category, `MaxConsecutiveFailures=3`, `FailureWaitSeconds`
non-zero: one attempt per check after each wait, a third failure leaves the request with
`"mode": "LeftForPerson"` and its category, and later checks do not attempt it. A manual advance with a
working generator then succeeds and the request is no longer left for a person.

## 6. Restart (automated, real temp file)

Record failures, restart the store/services over the same file: waiting time and hand-over still apply,
completed steps are not repeated, a version-1 file is upgraded in place and keeps its data.
