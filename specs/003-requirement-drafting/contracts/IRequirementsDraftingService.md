# Contract: IRequirementsDraftingService and IRequirementsDraftGenerator

Namespace `AgenticChangeOps.Intake.Services`. In-process .NET interfaces; there is no HTTP,
MCP, or CLI surface in this slice.

## IRequirementsDraftingService

```csharp
public interface IRequirementsDraftingService
{
    Task<DraftingResult> DraftAsync(Guid changeRequestId, CancellationToken cancellationToken = default);
    QueryResult<RequirementsDraft?> GetLatestDraft(Guid changeRequestId);
    QueryResult<IReadOnlyList<RequirementsDraft>> GetDraftHistory(Guid changeRequestId);
}
```

### DraftAsync

| Outcome | Result | Side effects |
|---------|--------|--------------|
| Valid content from generator | `Success(draft)` with `Version = previous + 1`, `Status = Draft` | One draft stored, one `DraftGenerated` audit entry; change request unchanged |
| Unknown id | `Failure([ChangeRequestId])` | None; generator not called |
| Status not `Submitted`/`Deferred` | `Failure([Status])`, message names the current status | None; generator not called |
| Generator throws, is cancelled, or returns null | `Failure([Generator])` | None; no version consumed |
| Generator throws `DraftGenerationException` (slice 004) | `Failure([Generator])`, message `The content generator failed (<Category>).` where Category is `Unreachable`, `TimedOut`, `ResponseTooLarge` or `MalformedOutput` | None; no version consumed |
| Content missing/empty/oversized sections | `Failure([<section>, ...])`, one error per failing section | None; no version consumed |
| Status changed to non-draftable during generation | `Failure([Status])` | None |

Guarantees: never modifies the `ChangeRequest`; never triggers on submission; a failed attempt
leaves the request, audit history, and earlier drafts identical to before.

### GetLatestDraft / GetDraftHistory

| Situation | `GetLatestDraft` | `GetDraftHistory` |
|-----------|------------------|-------------------|
| Unknown id | `Failure([ChangeRequestId])` | `Failure([ChangeRequestId])` |
| Request exists, no drafts | `Success(null)` | `Success([])` |
| Drafts exist | `Success(highest version)` | `Success(all, ascending version)` |

Retrieval is allowed for requests in any status.

## IRequirementsDraftGenerator

```csharp
public interface IRequirementsDraftGenerator
{
    Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken);
}
```

Behavioural contract for implementers:

- Produces content only. It must not call back into services, change status, or take any
  decision (Principle II).
- Receives the change request as stored; must not mutate it.
- May throw on failure or return `null`/incomplete content; the service handles both.
- Configuration (endpoint, deployment, key) comes from the environment at run time and is never
  committed (FR-021). No implementation ships in this slice; tests use `FakeDraftGenerator`.

## IChangeRequestStore additions

```csharp
void SaveDraft(RequirementsDraft draft, ChangeRequestAuditEntry auditEntry); // stores both or neither
IReadOnlyList<RequirementsDraft> GetDrafts(Guid changeRequestId);            // ascending Version
```
