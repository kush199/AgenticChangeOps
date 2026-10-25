# Verification: Reviewer Agent Reviews A Change Proposal

Recorded at T065 on `feature/reviewer-agent-review` (HEAD `86ccac4` plus the uncommitted step 5 documentation and
live-check changes), 2026-10-25. Follows quickstart.md sections 1 and 2; section 4 (live) is recorded as not run.

## Quickstart section 1: baseline

Recorded before any code change in `baseline-tests.txt` (1,644 tests on `69d8de8`, 0 failed, 4 skipped live checks) and
`baseline/README.md` (the existing golden transcripts and `openapi.json` already assert the recordings byte for byte).

## Quickstart section 2: each step green

| Step | Reviewer.Tests | Other new tests | Result |
|---|---|---|---|
| Phase 2 | (none) | | all existing projects at baseline, 0 failed |
| 1: pure logic | 156 | | all green |
| 2: prompt, parser, generator | 252 | ModelAccess +22 | all green |
| 3: service, store, audit | 349 | | all green |
| 4: registration | 403 | Api +78 | all green |
| 5: docs and live check | 403 | Api +1 skipped live test | all green |

Final run (`dotnet build AgenticChangeOps.sln -c Release`, then `dotnet build` and `dotnet test AgenticChangeOps.sln`):
build 0 warnings, 0 errors; 0 failed anywhere.

| Project | Passed | Skipped | Total |
|---|---|---|---|
| Intake.Tests | 118 | 0 | 118 |
| Persistence.Tests | 81 | 0 | 81 |
| Drafting.Tests | 45 | 0 | 45 |
| ModelAccess.Tests | 271 | 0 | 271 |
| SampleCode.Tests | 123 | 0 | 123 |
| Workflow.Tests | 194 | 0 | 194 |
| Agents.Requirement.Tests | 157 | 1 | 158 |
| Agents.Architect.Tests | 156 | 2 | 158 |
| Agents.Developer.Tests | 172 | 0 | 172 |
| Agents.Reviewer.Tests | 403 | 0 | 403 |
| Api.Tests | 423 | 2 | 425 |

The skipped tests are the opt-in live model checks (Requirement 1, Architect 2, Api 2: proposal and review).

## Quickstart section 3 scenarios

| Scenario | Proved by |
|---|---|
| Full chain, valid references: stored Draft review, three versions, system check, findings, open questions, one `ReviewGenerated`, status unchanged | `ReviewServiceStoreTests`, `ReviewCompositionTests` |
| Dangling reference among valid findings; no reference; too many; too large | `ReviewContentValidationTests`, `ReviewRejectionAndFailureTests` |
| Model failure, timeout, incomplete or malformed answer: nothing stored, category only | `ReviewGeneratorIntegrationTests`, `ReviewRejectionAndFailureTests` |
| Not Approved; missing or outdated requirements, design, proposal: specific error, model not called | `ReviewEligibilityTests` |
| Newer record saved while the model call runs: refused as outdated | `ReviewOutdatedDuringPreparationTests` |
| Sample folder missing: stored with the check not performed | `ReviewSampleUnavailableTests`, `ReviewCompositionTests` |
| Quote no longer matches: stored, outcome `QuoteNotFound` | `ReviewServiceStoreTests` |
| Model writes its own system check: ignored | `ReviewResponseParserTests`, `ReviewGeneratorIntegrationTests`, `ReviewServiceStoreTests` |
| Two reviews: versions 1 and 2, latest is 2, history in order, version 1 unchanged; overlapping requests get distinct versions | `ReviewRetrievalTests`, `ReviewCompositionTests` |
| Reviewer unused: existing responses byte-identical, route table and OpenAPI unchanged | the 15 `Baseline` golden tests, `ReviewOffMatchesBaselineTests` |

## Final scan (T066)

- No guard token (`CancellationTokenSource`, `JsonDocument`, `GetProperty(`, `IsNullOrWhiteSpace(`, a code fence), no
  `SampleCode`, framework or MCP name in `src/AgenticChangeOps.Agents.Reviewer`.
- No file under the Requirement, Architect and Developer agent projects, Drafting, Workflow, Persistence or SampleCode
  differs from `main`.
- Against `main`, the only existing test file modified is `golden/openapi.json` (one added audit value, approved at T004).
- One problem found and fixed while verifying: the first version of the live check named the plain Azure client, which an
  existing Requirement guard (`OutsideTheLiveFolder_NoTestCreatesTheRealClientOrReadsTheProcessEnvironment`) forbids outside
  its own live folder. The live check now takes the real generator from the host's composition instead; the guard is untouched.

## Quickstart section 4: live check

**Not run.** `LiveReviewCheckTests` exists (`tests/AgenticChangeOps.Api.Tests/Live/`) and is skipped unless
`AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1` with the model settings and a signed-in identity. No real model has been called
for this slice; the prompt wording and the real model's willingness to cite `document.list[position]` correctly are
unproven outside scripted answers.
