# Contract: Reference, Finding And Limit Rules

## Model answer shape

One JSON object with exactly these keys (extra keys are ignored and never stored):

- `findings`: list of objects with `category`, `severity`, `statement`, `references` (list of strings)
- `openQuestions`: list of strings (empty list if none)

Everything the model is shown (change request, three upstream records, the system check) follows a
delimiter line and is described as data, never instructions. Every list is numbered from 1.

## Shape rules (errors with the `MalformedOutput` category)

- No answer, or the answer is not the object above, or `findings` or `openQuestions` is missing.
- A finding with a blank or missing `statement`, `category` or `severity`, or a word outside the
  fixed sets (case-insensitive: categories `AcceptanceCriteria`, `DesignConsistency`,
  `UnmentionedRisk`; severities `Low`, `Medium`, `High`).
- Text is trimmed and blank list items are dropped, using the shared normaliser.

## Reference format

`document.list[position]`, case-insensitive on names, position a positive whole number.

| Document | Lists |
|---|---|
| requirements | functionalRequirements, acceptanceCriteria, assumptions, openQuestions |
| design | affectedComponents, impactAndRisks, dependencies, rollbackConsiderations, openQuestions |
| proposal | changes, risks, testIdeas, openQuestions |

Summaries are not referenceable. A reference resolves only if it parses, the list belongs to the
document, and the position is within the list in the exact upstream version used.

## Rejections (the whole review is rejected, nothing stored)

| Reason | When |
|---|---|
| `MissingReference` | a finding has no reference |
| `UnresolvedReference` | any reference does not parse, names an unknown list, or points past the end of a list |
| `MalformedFinding` | reserved for a finding that passed shape rules but cannot be typed |
| `TooManyFindings` | more findings than `MaxFindings` |
| `TooLarge` | UTF-8 bytes of statements plus open questions exceed `MaxTotalBytes` |

Check order: shape, count, size, then each finding references in order. The first reason found is the
one returned. Zero findings is valid.

## What a rejection reveals

A fixed reason name and a fixed message ("The review was rejected (UnresolvedReference)."). Never the
finding text, the reference text, a position, a path or any model text.
