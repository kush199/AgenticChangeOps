# Gap Review: existing assertions per service branch

Compared on commit `7bf6f44`. "HTTP" means the goldens in `tests/AgenticChangeOps.Api.Tests/Baseline/golden` or the new `drafting-skeleton-transcript.txt`.

| Branch of the sequence | Requirement | Architect | Developer |
|---|---|---|---|
| Unknown id, ineligible status | `DraftEligibilityTests` | `DesignEligibilityTests` | `ProposalEligibilityTests` |
| Generator never called when ineligible | `Draft_RefusedRequest_NeverCallsGeneratorAndStoresNothing` | `Design_RefusedForStatus_NeverCallsGeneratorOrBasisSourceAndStoresNothing` | `AnyStatusButApproved_IsRefused_AndTheBasisIsNotConsulted` |
| Category failure, nothing but the category | `DraftGenerationFailureTests`, `DraftingFailureCategoryTests` | `DesignGenerationFailureTests` | `ProposalRejectionTests.AGeneratorFailure_StoresNothing_AndReportsItsCategoryOnly` |
| Other exception, type name only | `GeneratorThrows_ReturnsGeneratorErrorWithoutLeakingTheMessage` | `GeneratorThrowsOtherException_ErrorNamesOnlyTheExceptionType` | `AnUnexpectedException_IsReportedByTypeNameOnly` |
| Caller's token cancelled, generator throws cancellation | **gap G1** (`GeneratorIsCancelled...` uses a live token) | **gap G2** | rethrown: `ACancelledRequest_StoresNothing` |
| Cancellation with a live token | `GeneratorIsCancelled_ReturnsGeneratorErrorInsteadOfThrowing` | **gap G2** | **gap G3** |
| Generator returns nothing | `GeneratorReturnsNull_...` | `GeneratorReturnsNull_IsMalformedOutput...` | `ProposalContentValidationTests` (null content) |
| Invalid content gives MalformedOutput | `BlankSummary_ResultCarriesMalformedOutput` | `IncompleteContent_IsMalformedOutput...` | `ProposalContentValidationTests` |
| Rejected content (Developer only) | n/a | n/a | `ProposalRejectionTests`, `ProposalVerifierTests` |
| Re-check inside the lock: status changed after generation | `Draft_ApprovedAndADraftArrivesDuringGeneration_...` | `Design_RequestStopsBeingApprovedDuringGeneration_...` | `TheStatusChanging_WhileGenerating_RefusesAndStoresNothing` |
| Currency check inside the lock | n/a (none) | `Design_RequirementsDraftChangesDuringGeneration_...` | `ANewerRequirementsDraft...`, `ANewerDesignDraft...` |
| Overlapping calls, distinct versions | `StoreConcurrencyTests` | `OverlappingRequests_TakeDistinctConsecutiveVersions...` | `OverlappingRequests_GetDistinctConsecutiveVersions_...` |
| Record and audit entry stored together | `DraftStoreTests`, `Retrieval_WhileDraftsAreBeingStored...` | `DesignVersioningTests` | `AReader_NeverSeesAProposalWithoutItsAuditEntry` |
| Reads, not found, latest null, history | `DraftRetrievalTests` | `DesignRetrievalTests` | `ProposalRetrievalTests` |
| Tool-call entries before the main entry | n/a | `SampleToolsAuditTests` | `OneToolCalledEntryPerCall_InOrder_ThenOneProposalGeneratedEntry`, `AToolTarget_HasControlCharactersRemoved_AndIsTruncated` |
| Automatic marker | `AutomaticMarkerTests` | `AutomaticMarkerTests` | n/a (none) |
| HTTP, all operations | `transcript.txt`, `shared-plumbing-transcript.txt`, `caller-cancellation-transcript.txt` | `design-operations-transcript.txt`, `shared-plumbing-transcript.txt`, `caller-cancellation-transcript.txt` | `drafting-skeleton-transcript.txt` (new, T005a) |

## Gaps and the characterization tests that close them (T007)

| Gap | Test (new file, green on the old code) |
|---|---|
| G1 Requirement: caller's token cancelled, generator throws cancellation, reported by type name, no category, nothing stored | `tests/AgenticChangeOps.Agents.Requirement.Tests/Characterization/CancellationCharacterizationTests.cs` |
| G2 Architect: same | `tests/AgenticChangeOps.Agents.Architect.Tests/Characterization/CancellationCharacterizationTests.cs` |
| G3 Developer: cancellation exception while the caller's token is live is an ordinary type-name failure | `tests/AgenticChangeOps.Agents.Developer.Tests/Characterization/CancellationCharacterizationTests.cs` |

No other branch lacks an assertion.
