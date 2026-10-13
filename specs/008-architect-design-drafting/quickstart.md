# Quickstart: Validating Architect Design Drafting

Prerequisites: .NET 8 SDK. No model settings are needed for any step except the optional live check.

## 1. Build and run the whole suite

```powershell
dotnet build AgenticChangeOps.sln -c Release
dotnet test AgenticChangeOps.sln -c Release --no-build
```

Expected: all earlier tests pass unchanged; the new `AgenticChangeOps.Agents.Architect.Tests` and design endpoint tests pass; the live check is reported skipped.

## 2. Check the isolation rules

```powershell
dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~StructureTests"
```

Expected: pass (see [contracts/dependency-rules.md](contracts/dependency-rules.md)).

## 3. Exercise the HTTP operations without a model

The API tests start the app in-process with a fake generator. They cover each row of [contracts/http-api.md](contracts/http-api.md):

```powershell
dotnet test tests/AgenticChangeOps.Api.Tests --filter "FullyQualifiedName~Design"
```

Expected: 201 with version 1 for an approved request with a requirements draft; 409 `Status` for each other status; 409 `RequirementsDraft` with none; 404 and 400 for unknown and malformed ids; 502 with `category` only for each model failure; nothing stored after any refusal or failure.

## 4. Manual end-to-end walk-through (optional, needs a model)

1. Set the Azure OpenAI environment variables as described in the README (never commit them).
2. Start the API: `dotnet run --project src/AgenticChangeOps.Api`.
3. Submit a synthetic change request, request a requirements draft, approve the request (human decision).
4. `POST /change-requests/{id}/designs` and confirm 201 and a `DesignGenerated` audit entry.
5. `GET /change-requests/{id}/designs/latest` and `GET /change-requests/{id}/designs`.

## 5. Opt-in live check

```powershell
$env:AGENTICCHANGEOPS_LIVE_MODEL_CHECK = "1"   # plus the model settings; same switch as the requirements live check
dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~Live"
```

Not run by default and not run in CI.
