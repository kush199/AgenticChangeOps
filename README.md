# AgenticChangeOps

A C# multi-agent system that processes enterprise software change requests through AI agents,
with **deterministic routing in code** and **human-approval gates** before any consequential
action.

This is a learning and portfolio project, built slice by slice with
[GitHub Spec Kit](https://github.com/github/spec-kit) (spec-driven development). It uses
**synthetic data only**; no corporate, client or proprietary material is in this repository.

## What it does today

| Capability | What it does |
|---|---|
| **Intake** | Accepts a change request, validates it (one error per failing field), assigns an ID, stores a typed record and writes an audit entry |
| **Approval gate** | A human approver records Approve, Reject or Defer with a reason. Approved and Rejected are final. `clearance` is true only for Approved |
| **Requirements drafting** | A Requirement agent asks a language model to draft structured requirements (summary, functional requirements, acceptance criteria, assumptions, open questions). Drafts are versioned and audited, and **never change the request's status** |
| **HTTP API** | All of the above over a local JSON API with a Swagger page |

Not built yet: the Architect, Developer and Reviewer agents, orchestration between agents,
authentication and persistent storage (data is held in memory and lost when the API stops).

## Design principles

The project is governed by a [constitution](.specify/memory/constitution.md). The key rules:

1. **Synthetic data only.** No real data, and secrets are never committed.
2. **Deterministic routing.** C# code decides what runs and when. The model only generates content.
3. **Human approval gates.** Consequential actions need a recorded human decision.
4. **Typed, auditable records.** Agent inputs and outputs are typed and traceable per change request.
5. **Spec-driven, slice by slice.** No code without an approved spec, plan and tasks.
6. **Test-first.** All model calls sit behind interfaces, and the automated tests never call a real model.
7. **Simplicity.** Four agents in V1, and nothing added without justification.

## How a request flows

```
HTTP client
   │  POST /change-requests
   ▼
Api  (AgenticChangeOps.Api)        endpoints, JSON errors, Swagger
   ▼
Intake  (AgenticChangeOps.Intake)  validation, approval rules, drafting rules, audit, store
   ▼
IRequirementsDraftGenerator ──► AzureOpenAI  (AgenticChangeOps.Intake.AzureOpenAI)
                                 prompt → one model call → parse → typed draft
```

## Repository layout

```
src/
  AgenticChangeOps.Api/                 HTTP API (minimal API + Swagger)
  AgenticChangeOps.Intake/              domain models, services, in-memory store
  AgenticChangeOps.Intake.AzureOpenAI/  model-backed draft generator
tests/
  AgenticChangeOps.Api.Tests/
  AgenticChangeOps.Intake.Tests/
specs/                                  one folder per slice: spec, plan, tasks, contracts
.specify/                               Spec Kit templates, scripts and the constitution
```

| Slice | Folder |
|---|---|
| Change request intake | `specs/001-change-request-intake` |
| Approval gate | `specs/002-change-request-approval-gate` |
| Requirements drafting | `specs/003-requirement-drafting` |
| Model-backed draft generator | `specs/004-model-draft-generator` |
| HTTP API | `specs/005-web-api` |

## Getting started

Requirements: [.NET SDK](https://dotnet.microsoft.com/download) 8 or later.

```powershell
git clone https://github.com/kush199/AgenticChangeOps.git
cd AgenticChangeOps
dotnet test
dotnet run --project src/AgenticChangeOps.Api
```

Open **http://127.0.0.1:5080/swagger** to try the operations. The API listens on the local
machine only and has no authentication.

### Try it from PowerShell

```powershell
$base = "http://127.0.0.1:5080"

$created = Invoke-RestMethod -Method Post -Uri "$base/change-requests" -ContentType "application/json" -Body (@{
    title          = "Rotate the sample service certificate"
    description    = "Replace the expiring certificate on the synthetic Orders service."
    requester      = "Sample Requester"
    affectedSystem = "Sample Orders Service"
    priority       = "High"
    changeType     = "ConfigChange"
} | ConvertTo-Json)

$id = $created.changeRequestId

Invoke-RestMethod -Method Post -Uri "$base/change-requests/$id/decisions" -ContentType "application/json" -Body (@{
    approverName = "Sample Approver"
    decision     = "Approve"
    reason       = "Low risk, scheduled window."
} | ConvertTo-Json)

Invoke-RestMethod "$base/change-requests/$id/clearance"   # isCleared: True
Invoke-RestMethod "$base/change-requests/$id/audit"
```

### Endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/change-requests` | Submit a change request |
| GET | `/change-requests/{id}` | Retrieve a change request |
| GET | `/change-requests/{id}/audit` | Audit history, in order |
| POST | `/change-requests/{id}/decisions` | Approve, Reject or Defer |
| GET | `/change-requests/{id}/clearance` | Is it cleared to proceed? |
| POST | `/change-requests/{id}/drafts` | Request a requirements draft (calls the model) |
| GET | `/change-requests/{id}/drafts/latest` | Latest draft |
| GET | `/change-requests/{id}/drafts` | Draft version history |

Errors are JSON with a stable `code` and never include stack traces, endpoints or secrets.

## Enabling requirements drafting (optional)

Every operation except drafting works with no cloud account. Without model settings, a draft
request returns `503 drafting-not-configured`.

To enable it you need an Azure OpenAI resource with a chat model deployment, and a signed-in
Azure CLI. Authentication is **keyless** (your `az login` identity, via the role
*Cognitive Services OpenAI User*), so no API key is read or stored.

```powershell
az login
$env:AGENTICCHANGEOPS_MODEL_ENDPOINT   = "https://<your-resource>.openai.azure.com/"
$env:AGENTICCHANGEOPS_MODEL_DEPLOYMENT = "<your-deployment-name>"
dotnet run --project src/AgenticChangeOps.Api
```

Optional: `AGENTICCHANGEOPS_MODEL_TIMEOUT_SECONDS` and `AGENTICCHANGEOPS_MODEL_MAX_RESPONSE_CHARS`.

Keep these values in your shell or a secrets manager. Never commit them. The automated tests
never call a real model; an opt-in live check runs only when
`AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1` is set.

## Roadmap

- Architect, Developer and Reviewer agents
- Orchestration between agents (Microsoft Agent Framework)
- MCP integration for external tools
- Persistent storage
- Authentication

## License

[MIT](LICENSE)
