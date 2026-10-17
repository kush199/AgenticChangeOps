# Quickstart: Validating The Sample Codebase Tools

Prerequisites: .NET 8 SDK. Nothing else for the automated checks.

## 1. Tools off is unchanged (no model needed)

```powershell
dotnet test
```

Expect every existing test to pass with no assertion edits, and the recorded design response
comparison (contracts/http-api-unchanged.md) to show no difference.

## 2. Confinement and limits (no model needed)

```powershell
dotnet test tests/AgenticChangeOps.SampleCode.Tests
```

Expect the escaping-path set (at least 20 spellings), the oversized-file, data-limit, call-limit
and time-limit cases, the read-only check, and the sample hygiene scan to pass.

## 3. Design with a scripted model (no model needed)

```powershell
dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~SampleTools"
```

Expect: a design lists only files the scripted model read; one audit entry per call with no
content; an empty list when nothing was read; `ToolsUnavailable` and `LimitsExhausted` store
nothing; an instruction-like line in a sample file changes neither the tool list nor any status.

## 4. Live check (optional, maintainer only)

Not part of CI. With the usual model endpoint and deployment settings exported:

```powershell
$env:AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED = "true"
dotnet run --project src/AgenticChangeOps.Api
```

Submit and approve a change request about the orders service, draft requirements, then request a
design. Expect a design whose `sampleFilesRead` lists files under `orders-service/`, and
`ToolCalled` entries in the audit listing without file content. Unset the switch and repeat: the
response has no `sampleFilesRead`.
