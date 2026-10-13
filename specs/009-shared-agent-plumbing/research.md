# Research: Shared Agent Plumbing

All items resolved; no NEEDS CLARIFICATION remains.

## R1. Which code is identical in behaviour?

**Decision**: Share exactly four things, found by line-by-line comparison of the two agents:

| Piece | Requirement file | Architect file | Difference found |
|---|---|---|---|
| Model call, bound, size/blank checks, failure mapping | `AzureOpenAIDraftGenerator.CallModelAsync` + checks in `GenerateAsync` | `AzureOpenAIDesignGenerator.CallModelAsync` + checks in `GenerateAsync` | Identical apart from type names |
| Unwrap bare or fenced answer | `DraftResponseParser.Unwrap` | `DesignResponseParser.Unwrap` | None |
| Strict string and list reads | `ReadString`, `ReadList` | `ReadString`, `ReadList` | None |
| Text and list normalisation | `DraftContentValidator.NormaliseList` + summary check | `DesignContentValidator.NormaliseList` + summary check | Section names only; error text follows the same pattern per section |

**Rationale**: Rule: only identical behaviour is shared. These four have no behavioural difference.

**Alternatives considered**: sharing the whole generator through a base class (rejected: the generate entry point has a different shape, one adds the basis; a base class would be a framework abstraction with no third caller); sharing the services (rejected: out of scope, and the clarification keeps even the failure-translation step local).

## R2. Where do the shared pieces live?

**Decision**: `AgenticChangeOps.ModelAccess`.

**Rationale**: Both agents already reference it; it has no project references; it already contains `IChatModelClient`, `AzureOpenAIGeneratorOptions`, `ModelFailureMapper`, `DraftGenerationException`, `GenerationFailureCategory`, which the shared model call needs. Its tests already forbid the words `Requirement`, `RequirementsDraft`, `DraftPromptBuilder`, `AgenticChangeOps.Intake`, `AgenticChangeOps.Agents`. No dependency between agents is added.

**Alternatives considered**: a new "shared" project (rejected: a fifth source project for no gain, and ModelAccess is already that place); putting them in Intake (rejected: agents must not make intake carry model concerns, and ModelAccess does not reference Intake).

## R3. How does shared list normalisation report errors without the intake validation type?

**Decision**: `SectionNormaliser` returns `SectionOutcome<T>(Value, Error)` where `Error` is the exact message string used today. The agent wraps a non-null error into `new ValidationError(section, error)`.

**Rationale**: ModelAccess must not reference Intake (rule D3, tests forbid the namespace). The field name passed to `ValidationError` is the section name the agent already supplies, so the produced errors are identical, in the same order.

**Alternatives considered**: moving `ValidationError` out of Intake (rejected: changes a type used by intake and the API, breaks "no moved record" simplicity); referencing Intake from ModelAccess (rejected: violates neutrality rules and the existing test).

## R4. How do existing tests stay unchanged?

**Decision**: Keep the six agent entry points under their current names, namespaces, signatures and project. Tests that call `DraftResponseParser.Parse`, `new DesignContentValidator().Validate`, or `new AzureOpenAIDesignGenerator(options, client)` compile and run as before. The `StructureTests` "declared once, only in the agent project" lists remain true.

**Rationale**: Zero assertion changes and zero reference changes is the strongest form of FR-015, and it keeps the agent location tests meaningful.

**Alternatives considered**: deleting the adapters and calling the shared reader from the generator (rejected: edits many test references and the structure test lists, for a smaller line count that the adapters barely affect).

## R5. How is the model-backed behaviour proven unchanged over HTTP?

**Finding**: `ApiBaselineTests` and the design API tests use fake generators, so no HTTP test runs a real generator, parser or normaliser. Today the shared behaviours are exercised only by the agent unit tests.

**Decision**: Add `SharedPlumbingBaselineTests` in `Api.Tests/Baseline`. It registers the real `AzureOpenAIDraftGenerator` and `AzureOpenAIDesignGenerator` with a scripted `IChatModelClient` (through the existing `ApiFactory` generator parameter and `DesignHost` service override, so no production change), then drives both agents' endpoints through: valid bare answer, valid fenced answer, labelled-other fence, unterminated fence, non-object, wrong value type, missing key, blank required section, oversized answer, cut-off answer, empty answer, caller cancellation, time-limit expiry (very small timeout), client throwing a categorised failure, client throwing an arbitrary exception whose message carries a sentinel endpoint and deployment. It records status, headers, normalised body and error code, plus captured log text, into a golden file written **before** the extraction and compared after.

**Rationale**: This is the only way to satisfy "any recorded response differs" for the shared paths. It reuses the existing golden mechanism and normaliser.

**Alternatives considered**: relying on agent unit tests alone (rejected: they assert on service results, not on HTTP responses, logs or headers).

## R6. How is duplication measured?

**Decision** (clarified): count of production-code lines that appear in both agents after normalising. Script `specs/009-shared-agent-plumbing/measure-duplication.ps1`, run unchanged before and after.

Normalisation, in order, per `*.cs` file under each agent's `src` folder (excluding `bin`, `obj`):
1. Trim each line; drop blank lines, `//` and `///` comment lines, `using` and `namespace` lines, and lines made only of braces, brackets, parentheses, commas or semicolons.
2. Replace agent-specific words (`Requirements`, `Requirement`, `Architect`, `Design`, `Draft`, `Basis`) with empty text so that `DraftContent` and `DesignContent` compare equal.
3. Count the multiset intersection of the two agents' remaining lines (each line counts as many times as it appears in both).

Report: lines per agent, shared-line count, and shared lines as a percentage of the smaller agent. Prototype before the slice: about 240 shared lines (293 and 340 normalised lines).

**Rationale**: No tool or dependency, reproducible by hand, and it shows the copies disappearing. The headline figure includes the two services' shared skeleton, which this slice deliberately leaves, so the "after" figure will not be zero; the report explains the remainder by pointing to the register.

**Alternatives considered**: external duplicate detectors (rejected in clarification: adds tooling); whole-type comparison (rejected: coarser).

## R7. Which test helpers are shared?

**Decision**: Leave the two agents' `FakeChatModelClient` and `LiveModelFactAttribute` copies in place; add one small neutral `ScriptedChatModelClient` in `ModelAccess.Tests` for the shared tests. Record both in the register.

**Rationale**: FR-019 is a MAY. The two fakes differ by the agent's sample answer text, the attribute is a few lines, and sharing needs a new test-support project (a fifth test project) or a link across test projects. Neither is worth it for this slice. The register records it so a later slice can decide.

## R8. What about agent tests that now prove a shared behaviour a second time?

**Decision**: Keep them. FR-016 says each behaviour is proven once in the shared tests, and forbids deleting or weakening agent tests to achieve that. A list of the agent tests that are now redundant with a shared test is recorded in the final report for a later slice.

## R9. Test-first for a refactor

**Decision**: Shared tests are written against compile-time stubs of the three shared types (methods throw `NotImplementedException`), shown red, and approved before the stubs are filled in. The HTTP baseline test is written and run green against the unchanged code first (its goldens are the "before").

**Rationale**: Matches the project's approval checkpoint: show red tests and wait for "approved".
