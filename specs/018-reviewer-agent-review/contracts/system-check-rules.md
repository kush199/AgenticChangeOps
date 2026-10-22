# Contract: System Check Rules

Computed by the system, before the model call, without any model. Read-only.

## Inputs

The proposal edits, in order, and `IReviewSampleFiles` (owned by the Reviewer, implemented over the
sample's own path and read rules in the composition root).

## Availability

If `IsAvailable` is false: `Status = NotPerformed`, `Edits` empty, and the review continues.
Otherwise `Status = Performed` and there is one `EditCheck` per edit.

## Per-edit outcome

Line endings are the only thing that is not significant (CRLF, CR and LF compare equal). Every check
is against the file as it is now, never against another edit's result.

| Edit kind | Condition | Outcome |
|---|---|---|
| Change | existing text is empty | `MalformedEdit` |
| Change | path escapes the sample | `PathOutsideSample` |
| Change | file is missing, a folder, or unreadable | `FileNotFound` |
| Change | quote appears nowhere | `QuoteNotFound` |
| Change | quote appears more than once | `QuoteAmbiguous` |
| Change | quote appears exactly once | `Found` |
| Add | existing text is present | `MalformedEdit` |
| Add | path escapes the sample | `PathOutsideSample` |
| Add | path exists (file or folder) | `NewFileExists` |
| Add | path does not exist | `NewFileFree` |

## Guarantees

- The model answer cannot change the result: the stored `SystemCheck` is the one computed here.
- Only position and outcome are recorded; never a path, a quote or file text.
- Nothing is created, changed, deleted, compiled or run; files are only opened for reading.
- Any outcome other than `Found` or `NewFileFree` is material for findings, not a reason to reject
  the review.
