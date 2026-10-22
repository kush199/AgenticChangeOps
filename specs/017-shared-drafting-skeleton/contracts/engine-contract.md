# Contract: Drafting Engine

## Order of steps in `RunAsync` (identical today in all three services)

1. `FindById`. Ask the agent `CheckEligibility`. On error return failure (no category).
2. Call the agent's generator.
   - A `DraftGenerationException` becomes a `Generator` error `The content generator failed (<Category>).` with that category.
   - Cancellation requested by the caller with `PropagatesCancellation` true: rethrown.
   - Any other exception becomes `The content generator failed (<ExceptionTypeName>).`, no category, no message text.
3. `Review`. Invalid: failure with `MalformedOutput`. Rejected: outcome rejected with the agent's reason.
4. Take `SyncRoot`. No await inside until released:
   1. `FindById` again; `CheckEligibility` again; an error returns failure.
   2. `CheckStillCurrent` (agent); an error returns failure.
   3. Read the clock once; version = `CountExisting + 1`.
   4. Run the save delegate; return success with its record.

Reads: not-found error if the request is missing; otherwise read under `SyncRoot`; latest is last or null.

## Guarantees

- Two overlapping runs never take the same version.
- A record and its main audit entry are stored together or not at all (the store's atomic save is called inside the lock).
- Nothing but the category or exception type name leaves a generator failure.
- The engine contains no agent name, store collection, record shape or approval type.

## What an agent supplies

See data-model.md. A fourth agent writes its own `IDraftingAgent`, its service and its save delegate, and edits nothing else.
