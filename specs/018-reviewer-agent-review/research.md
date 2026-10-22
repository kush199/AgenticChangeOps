# Research: Reviewer Agent Reviews A Change Proposal

## R1. Can the slice 017 engine run the Reviewer unchanged?

Compared the engine's six steps with the Reviewer's needs. All fit: look up, eligibility, generate,
translate failure, review, locked re-check and save, plus latest and history reads.

Decision: use `DraftingEngine<Review, Bases, GeneratedReview, ValidatedReview, ReviewRejection>`
unchanged. The `Review` step receives only the generated value, but reference resolution needs the
upstream records the model was given. `GeneratedReview` therefore carries the content, the system
check and the bases used; the agent builds it inside its generate step. Alternatives: change the
engine to pass the basis to review (edits a shared piece for one caller, and the spec forbids
changing other agents' behaviour); a field on the agent (shared mutable state across overlapping
calls, rejected).

## R2. Where is the system check computed, and who sees it?

Clarification Q2: the model receives it. Decision: computed in the agent's generate step before the
model call, from the proposal's edits and `IReviewSampleFiles`. It is stored exactly as computed.
The model's JSON schema has no key for it; unknown keys are ignored and never stored. Alternative:
compute after the model (rejected by Q2).

## R3. What does each edit's check say?

Same rule as the Developer's verifier, recorded as an outcome per edit instead of a rejection:
`Found` (change quote appears exactly once), `QuoteNotFound`, `QuoteAmbiguous`, `FileNotFound`,
`PathOutsideSample`, `NewFileFree` (add, path does not exist), `NewFileExists`, `MalformedEdit`.
Unlike the Developer, a bad outcome does not reject the review; it is the finding material. Overlap
between edits is not re-checked (the proposal already passed it when stored). Only the edit's
position and outcome are stored, never a path, a quote or file text.

## R4. What is "sample unavailable"?

The sample folder is missing or unreadable at check time. Decision: `IReviewSampleFiles.IsAvailable`
false gives `SystemCheckStatus.NotPerformed` with an empty edit list, and the review continues. A
single file that cannot be read gives that edit `FileNotFound`, not "not performed". Alternative:
treat any read failure as not performed (hides a real mismatch).

## R5. Reference format and resolution

Clarification Q1: a specific list entry. Decision: the model writes `document.list[position]` with
position 1-based, and the system parses it into `ReviewReference(Document, List, Position)`.
Valid lists per document:

- requirements: `functionalRequirements`, `acceptanceCriteria`, `assumptions`, `openQuestions`
- design: `affectedComponents`, `impactAndRisks`, `dependencies`, `rollbackConsiderations`, `openQuestions`
- proposal: `changes`, `risks`, `testIdeas`, `openQuestions`

Resolved against exactly the versions the model was given. A reference that does not parse, names
an unknown list, or points past the end of a list is unresolved. Summaries are not referenceable
(they are single text, not list entries). The prompt numbers every list so the model can cite them.
Alternative: ids on every entry (changes the other agents' records, out of scope).

## R6. What happens on a bad reference or an over-limit review?

Decision, matching the spec: the whole review is rejected with a typed reason
(`UnresolvedReference`, `MissingReference`, `TooManyFindings`, `TooLarge`, `MalformedFinding`) and
nothing is stored. Shape problems (missing section, blank statement, unknown severity or category
word) are `Invalid` errors with the `MalformedOutput` category, as for the other agents. A finding
with no reference is a rejection (`MissingReference`) because the spec calls it out as a rule, not a
shape error.

## R7. Finding categories and severities

Decision: categories `AcceptanceCriteria`, `DesignConsistency`, `UnmentionedRisk` (the spec's three
topics); severities `Low`, `Medium`, `High`. Both are enums parsed case-insensitively from words;
anything else is a shape error. Nothing in the system reads either value except the validator and
the prompt. Zero findings is valid (spec edge case).

## R8. Limits

Decision: `MaxFindings` default 8, `MaxTotalBytes` default 8000 (UTF-8 bytes of finding statements
and open questions). Operator-set through two optional variables, read when the review service is
first used, positive whole numbers only, reported by setting name only, mirroring the proposal
limits.

## R9. Eligibility order

Existence, then status (the basis source is not consulted for a request that is not Approved), then
requirements draft, design draft, design based on latest requirements, proposal, proposal based on
latest design and latest requirements. Distinct field names per condition so the specific error is
visible. A proposal based on an older design or requirements version is the "outdated" error, in
the Developer's wording style.

## R10. Currency while preparing

The engine's locked step calls the agent's `CheckStillCurrent` with the bases used and the current
ones; the Reviewer compares requirements, design and proposal versions and returns the outdated
error. Nothing is stored. Overlapping calls cannot share a version (engine).

## R11. Model client and constitution

The Requirement agent calls the model through the plain `AzureOpenAIChatModelClient`; the Architect
and Developer use framework-backed clients for the tool studies. The Reviewer needs no tools, so it
uses the plain client and the Technology Stack text needs no amendment. Alternative: the framework
client, which would need its own spec and amendment under the existing wording.

## R12. Does a live check belong in this slice?

Same as earlier slices: a `Live` test class exists, skipped unless the maintainer sets the model
variables. The result is reported honestly as not run unless it was.

## R13. Registration without an endpoint

The spec adds no HTTP operation, but "the system's internal service" must be obtainable. Decision:
register `IReviewService` in the composition root with a lazily built setup (mirrors `DesignSetup`):
when the model configuration is invalid the generator is a "not configured" one that fails with the
`NotConfigured` category. Nothing resolves it unless a caller asks, so a server with the Reviewer
unused starts and behaves exactly as before. A test proves the existing routes are byte-identical.
