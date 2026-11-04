# Data Model: A Change Proposal Requires An Accepted Design

No new stored record, table, audit event or layout change.

## ProposalDesignVerdict (new enum, Developer, not persisted)

`None`, `Accepted`, `Rejected`: the decision on the *latest* design version only.

## ProposalDesignBasis (additive member)

`Decision : ProposalDesignVerdict = None`, appended last with a default; every existing construction stays
valid.

## Validation field

New `DesignAcceptance` field name on `ValidationError`, with two messages: design not decided; design rejected.

## State read

`DesignDecision` (slice 022), matched by `Version == latest design Version`. Read only.

## Check order

Exists and Approved -> requirements draft -> design draft -> design based on latest requirements ->
(readiness) decision Accepted -> generation. At storing: re-run eligibility, then currency (requirements
version, design version, decision Accepted), otherwise outdated.
