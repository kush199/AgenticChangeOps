# Duplication Report: Shared Agent Plumbing

Method: `measure-duplication.ps1` in this folder (research.md R6, clarified method). Production code only; names, blank lines, comments, `using`, `namespace` and brace-only lines normalised away. The same script runs before and after.

## Before and after

| Figure | Before (commit `a49108f`) | After |
|---|---|---|
| Requirement agent normalised lines | 293 | 212 |
| Architect agent normalised lines | 340 | 259 |
| Lines shared between the two agents | **240** | **155** |
| Shared as a percentage of the smaller agent | 81.9% | 73.1% |
| New agent-neutral lines in ModelAccess (4 files) | n/a | 109 |
| Total production lines, both agents plus shared pieces | 633 | 580 |

Shared lines fell by 85 (35.4%). The three duplicated behaviours (model call, answer reading, section normalising) now exist once, in 109 lines, instead of twice (about 162 lines).

## What the remaining 155 shared lines are

Counted per Requirement-agent file (each is matched by a similar line in the Architect agent):

| Lines | File | Why it stays (register entry) |
|---|---|---|
| 70 | `RequirementsDraftingService` | Service skeleton: eligibility, failure translation, save step, queries (K1 to K4). Merging the services is out of scope. |
| 20 | `DraftingResult` | Result record and its `Success`/`Failure` factories, one per agent (K11). |
| 15 | `DraftContentValidator` | Null-content check and the `Check` helper that wraps a shared error in an intake validation error (K5, K12). |
| 13 | `RequirementsDraft` | Persisted record shape, different per agent (K8). |
| 12 | `DraftPromptBuilder` | Prompt text and its assembly; any prompt change is out of scope (K7, K13). |
| 8 | `AzureOpenAIDraftGenerator` | Constructor and factory, different names used by callers (K6). |
| 12 | Interfaces, status, content, store extensions, parser adapter | Agent-specific types and the adapter's section names (K8). |

Every line in the shared place is used by both agents; none of the 155 is a copy of a shared behaviour (structure tests N4 enforce that).

## Verification

Recorded 2026-10-13 after the extraction.

| Check | Result |
|---|---|
| Existing tracked test files changed (`git diff HEAD -- tests`) | none (0 files) |
| Existing goldens changed (`git diff HEAD -- tests/.../Baseline/golden`) | none; `transcript.txt` and `openapi.json` untouched, `shared-plumbing-transcript.txt` added before the extraction and unchanged after |
| Project files changed (`git diff HEAD -- '*.csproj' '*.sln'`) | none; no new package, project or reference |
| Intake, Api, both services, prompt builders, content and record types | untouched |
| Full suite | 0 failed; every earlier test passes; skipped is still the two opt-in live checks |
| Secret, endpoint and leak scans | green (ModelAccess 7, Architect 2, Api 8) |
| Intentional differences still true in code | yes: required sections (Requirement: functional requirements, acceptance criteria; Architect: affected components, impact and risks, rollback considerations; the other list sections optional), eligible status (Submitted or Deferred versus Approved), basis check, audit events (`DraftGenerated` versus `DesignGenerated`), generator entry points, factory names (`CreateFromEnvironment` versus `FromProcessEnvironment`) |

## Third agent walk-through

A new agent project referencing Intake and ModelAccess does three things and edits nothing that exists:

1. `new ModelCaller(options, client)` once, then `await caller.GetAnswerTextAsync(system, user, ct)` with its own prompt.
2. `ModelAnswerReader.Read(text, a => new MyContent(a.ReadString("..."), a.ReadList("...")))` with its own section names.
3. `SectionNormaliser.Text(...)` and `SectionNormaliser.List(..., requireItem: ...)` for its own sections, turning any returned error string into its own validation result.

No file in ModelAccess, `Agents.Requirement` or `Agents.Architect` needs an edit. `StandInAgentTests` is the executable proof (it references only ModelAccess), and `SharedNeutralityTests` keeps the shared code from naming any agent.

## Redundant agent tests

These agent tests now prove a behaviour that a shared test also proves (research.md R8). They are kept as they are (FR-016) and listed for a later slice:

| Agent test | Behaviour also proven once in the shared tests |
|---|---|
| `DraftResponseParserTests`, `DesignResponseParserTests` (fence, type and key cases) | `ModelAnswerReaderTests` |
| Model-call cases of `DraftGeneratorFailureTests`, `DesignGenerationFailureTests` (category pass-through, mapping, time limit, size limit, blank answer, no detail attached) | `ModelCallerTests` |
| List and text limit cases of `DraftContentValidationTests`, `DesignContentValidationTests` | `SectionNormaliserTests` |

Each agent test also still proves something the shared tests cannot: that this agent's section names, required choices and prompt feed the shared pieces correctly.

## Test code (reported separately)

Same normalisation, applied to the two agents' test projects with `measure-duplication.ps1 -Scope Tests`. The headline figure above is production code only (spec Assumptions); this section is for information.

| Figure | Before (`a49108f`, via `-Root` on a worktree) | After |
|---|---|---|
| Requirement test lines | 1372 | 1372 |
| Architect test lines | 1096 | 1096 |
| Lines shared between the two test projects | 545 | 545 |
| Shared as a percentage of the smaller project | 49.7% | 49.7% |

The two agents' test projects are unchanged by this slice. The shared-behaviour scan (rule N4) was first written as one file per agent (T013), which added 31 identical lines to each project (545 to 576). Convergence (T044) replaced the pair with a single `AgentsHoldNoSharedBehaviourCopyTests` in `ModelAccess.Tests`, which scans every `src/AgenticChangeOps.Agents.*` folder, so the figure is back to the pre-slice value and a third agent is covered without any test edit.

The shared test lines that remain are the existing, unchanged duplicates already named in K9 and K10 (fakes, live-check attribute, and the parallel structure and unit tests of the two agents). The new test code of this slice lives in `ModelAccess.Tests` and `Api.Tests`, not in either agent's test project; the pair in K15 is across those two.
