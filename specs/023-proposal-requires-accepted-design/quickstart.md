# Quickstart: validating the gate

Prerequisite: `dotnet test` is green on the branch.

1. Approve a change request; draft requirements and design (fake generators in tests).
2. POST a proposal: 409, field `DesignAcceptance`, "not decided"; fake generator call count 0; no proposal; no audit entry.
3. POST a design decision Reject, then POST a proposal: 409, "rejected" (a different message).
4. Request a new design (v2), then POST a proposal: 409 "not decided" (v1's decision is ignored).
5. Accept v2, then POST a proposal: 201, identical to the behaviour before this feature.
6. Existing refusals (unknown, not Approved, no requirements, no design, stale design) keep their reasons and order even when the design is undecided.
7. Start a proposal, add a newer design before storing: 409 outdated; nothing stored; earlier proposals untouched.

No live model check is needed: the gate runs before the model.
