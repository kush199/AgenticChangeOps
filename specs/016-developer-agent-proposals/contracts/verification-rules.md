# Contract: Proposal Verification

Run by the system on the model's content, in this order, before anything is stored. The first failure
rejects the whole proposal; nothing is partially accepted.

1. **Shape** (validator, shared normalising rules): summary present; at least one risk; every change
   has a path, a kind, replacement text; `Change` has existing text, `Add` has none. Failure:
   `MalformedOutput` (model-output failure, as for designs) for missing or wrongly typed sections,
   `MalformedEdit` for a kind and text mismatch.
2. **Count**: zero changes is `NoChanges`; more than `MaxChanges` is `TooManyChanges`.
3. **Size**: total UTF-8 bytes of all existing and replacement text over `MaxTotalBytes` is `TooLarge`.
4. **Path**, per edit: relative to the sample, resolved by the sample path rules. Anything the rules
   refuse (parent traversal, absolute, drive-qualified, link out, too long, control characters) is
   `PathOutsideSample`.
5. **Existence**, per edit:
   - `Change`: the file must exist and be readable text within the per-file size limit, else
     `FileNotFound`.
   - `Add`: nothing may exist at the path (file or folder), else `NewFileExists`. The parent location
     must still resolve inside the sample.
6. **Quote**, per `Change` edit: after normalising line endings to LF on both sides, the existing text
   must occur in the file exactly once. Zero is `QuoteNotFound`, more than one `QuoteAmbiguous`.
   Comparison is ordinal and case-sensitive; whitespace is significant.
7. **Overlap**: two `Change` edits to one file whose quoted ranges overlap, or two `Add` edits to one
   path, are `OverlappingEdits`. Edits are always checked against the original file.

Then, inside the store lock: eligibility again, requirements and design versions equal to the ones the
model was given, version assigned, tool-call entries and the `ProposalGenerated` entry saved with the
proposal in one step.

## Guarantees

- Verification reads the sample only; it never writes, creates, deletes, compiles or runs anything.
- A rejection carries only the reason code. No path, quote, replacement or file content is returned,
  logged or audited.
- Hostile text inside a file is only ever compared against; it cannot change the order, the limits or
  the outcome of these steps.
