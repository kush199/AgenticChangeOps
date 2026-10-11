# Research: Change Request Intake

**Feature**: 001-change-request-intake | **Date**: 2026-10-11

## Decisions

### 1. Submission Result Type

**Decision**: A `SubmissionResult` class with `bool IsSuccess`, `ChangeRequest? ChangeRequest`,
and `IReadOnlyList<ValidationError> Errors`. Two static factory methods:
`SubmissionResult.Success(ChangeRequest)` and `SubmissionResult.Failure(IReadOnlyList<ValidationError>)`.

**Rationale**: Simplest type-safe discriminated result in C# without language features
(e.g., discriminated unions) not available in C# 12. Spec requires the two states are
mutually exclusive; the factory-method pattern enforces this at construction time. No
external dependency required.

**Alternatives considered**:
- `OneOf<ChangeRequest, ValidationFailure>` (OneOf NuGet) — rejected; adds production
  dependency for a pattern easily expressed with a plain class.
- Throw on failure — rejected; spec explicitly requires structured error responses, not
  exceptions.
- Generic `Result<T, E>` — rejected; over-engineered for a single use case (Principle VII).

---

### 2. Not-Found Contract for Retrieval and Audit Listing

**Decision**: Nullable reference returns — `ChangeRequest? GetById(Guid)` and
`IReadOnlyList<ChangeRequestAuditEntry>? GetAuditHistory(Guid)`. Null signals not-found.
The caller already holds the ID and can compose a meaningful message.

**Rationale**: For an in-process .NET library, nullable returns are the idiomatic, zero-
overhead not-found signal. The spec says "no unhandled exception leaked" — null satisfies
this. A wrapper result type adds structure with no real benefit for a library caller.

**Alternatives considered**:
- `KeyNotFoundException` — rejected; spec prohibits leaked exceptions.
- `RetrievalResult<T>` wrapper — rejected; adds a type per operation with negligible benefit
  over nullable in a library context (Principle VII).

---

### 3. Infrastructure Seams for Test Determinism

**Decision**: Two thin interfaces injected into `ChangeRequestService`:
- `ISystemClock` with `DateTime UtcNow { get; }` — controls submission timestamps in tests.
- `IIdGenerator` with `Guid NewId()` — controls `ChangeRequestId` and `AuditEntryId`
  generation in tests.

Production implementations (`SystemClock`, `GuidIdGenerator`) delegate to `DateTime.UtcNow`
and `Guid.NewGuid()` respectively.

**Rationale**: Without these seams, timestamp and GUID assertions in unit tests are either
skipped or use approximate comparisons, leaving gaps in deterministic coverage (Principle VI).
With them, tests can assert exact values.

**Alternatives considered**:
- Mock `DateTime.UtcNow` with a shim — rejected; requires third-party mocking of a static
  member; fragile and framework-specific.
- Skip time/ID assertions — rejected; SC-005 requires exact timestamp ordering.

---

### 4. Validation Strategy

**Decision**: Manual validation in a dedicated `ChangeRequestValidator` class. Each rule
produces a `ValidationError(string FieldName, string Message)`. The validator returns
`IReadOnlyList<ValidationError>` (empty = valid). All rules run before returning (collect-all
semantics), satisfying FR-012 (one error per failing field).

**Rationale**: No production NuGet dependency. All validation rules are simple (required,
max-length, enum membership) and easily expressed as plain C#. A dedicated class keeps
`ChangeRequestService` focused on orchestration.

**Alternatives considered**:
- FluentValidation — rejected; production dependency for trivial rules (Principle VII).
- Inline in service — rejected; mixes orchestration and validation concerns.

---

### 5. Store Backing Type

**Decision**: Two `Dictionary<Guid, T>` fields — one for `ChangeRequest` records, one for
`List<ChangeRequestAuditEntry>` per `ChangeRequestId` — inside `InMemoryChangeRequestStore`.

**Rationale**: Single-threaded access is an explicit spec assumption. `Dictionary` is the
simplest in-memory lookup with O(1) access. `ConcurrentDictionary` would add unnecessary
overhead and imply thread-safety guarantees the spec explicitly defers.

**Alternatives considered**:
- `ConcurrentDictionary` — rejected; premature thread-safety (Principle VII; spec assumption
  explicitly defers concurrency to a future slice).
- SQLite in-memory — rejected; introduces an external dependency and defeats the "in-process"
  simplicity goal of this slice.

---

### 6. .NET Project Layout

**Decision**: Two projects in a single solution file at the repository root:
- `src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj` — `net8.0` classlib
- `tests/AgenticChangeOps.Intake.Tests/AgenticChangeOps.Intake.Tests.csproj` — `net8.0`,
  references xUnit 2.x

No production NuGet dependencies in the library project. Test project references xUnit and
the library project.

**Rationale**: Constitution mandates .NET 8. Two-project layout (src + tests) is the
conventional .NET structure that keeps the library pure and the test assembly separate.

**Alternatives considered**:
- `netstandard2.1` target — deferred; .NET 8 is the constitution-mandated runtime.
- Single-project with tests — rejected; mixes production and test code.
