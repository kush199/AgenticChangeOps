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
| **Design drafting** | An Architect agent asks a language model to draft a design analysis (approach, affected components, impact and risks, dependencies, rollback, open questions) for an **Approved** request that has a requirements draft. Designs are versioned, linked to the requirements version they used, audited as `DesignGenerated`, and **never change the request's status** |
| **Durable storage** | Change requests, audit entries, requirements drafts and design drafts are kept in a local SQLite file and survive a restart. A stored item and its audit entry are stored together or not at all, and version numbers never repeat |
| **HTTP API** | All of the above over a local JSON API with a Swagger page |

Not built yet: the Developer and Reviewer agents, orchestration between agents,
authentication.

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
Intake  (AgenticChangeOps.Intake)  validation, approval rules, audit, store
   │                               knows no agent; stores agent output through one generic point
   ▼
Requirement agent  (AgenticChangeOps.Agents.Requirement)   Architect agent  (AgenticChangeOps.Agents.Architect)
   │  drafting rules, prompt → one model call → parse → typed draft (each agent the same way)
   ▼
Model access  (AgenticChangeOps.ModelAccess)
                                   model seam, Azure OpenAI client, settings, failure mapping,
                                   the shared model call, answer reading and section normalising, and
                                   the Architect agent's framework-backed client (Microsoft Agent Framework)
```

Dependencies point one way: an agent depends on Intake and ModelAccess; Intake and ModelAccess
depend on no agent, and ModelAccess does not depend on Intake. Persistence depends on Intake only and
names no agent; the Api chooses which store to use.

## Repository layout

```
src/
  AgenticChangeOps.Api/                 HTTP API (minimal API + Swagger); the composition root
  AgenticChangeOps.Intake/              change requests, approval gate, audit, in-memory store
  AgenticChangeOps.Persistence/         durable local storage (SQLite); depends on Intake only
  AgenticChangeOps.ModelAccess/         agent-neutral model access: seam, Azure client, settings, shared call and parsing
  AgenticChangeOps.Agents.Requirement/  everything specific to the Requirement agent
  AgenticChangeOps.Agents.Architect/    everything specific to the Architect agent
tests/
  AgenticChangeOps.Api.Tests/                  HTTP behaviour, recorded baseline of responses
  AgenticChangeOps.Intake.Tests/               intake and approval, no agent or model references
  AgenticChangeOps.Persistence.Tests/          one store contract run against both stores, restart, atomicity, layout
  AgenticChangeOps.ModelAccess.Tests/          settings, failure mapping, hygiene and neutrality checks
  AgenticChangeOps.Agents.Requirement.Tests/   drafting, validation, prompt, parser, generator
  AgenticChangeOps.Agents.Architect.Tests/     design drafting, validation, prompt, parser, generator
specs/                                  one folder per slice: spec, plan, tasks, contracts
.specify/                               Spec Kit templates, scripts and the constitution
```

### Adding an agent

Each agent is one project beside the Requirement agent. Adding one needs no edit to the
Requirement agent or to ModelAccess:

1. Create `src/AgenticChangeOps.Agents.<Name>`, referencing `AgenticChangeOps.Intake` and
   `AgenticChangeOps.ModelAccess` (never another agent). Put the agent's records, validation,
   service, interface, prompt, section names and generator and its output type (implementing
   `IAgentOutput`) there. Use `ModelCaller`, `ModelAnswerReader` and `SectionNormaliser` from
   ModelAccess for the model call, answer reading and list/text limits instead of copying them.
2. Create `tests/AgenticChangeOps.Agents.<Name>.Tests` and add both projects to the solution.
3. Reference the new project from `AgenticChangeOps.Api` and register its services in
   `Composition/ApiServiceRegistration.cs`, the one place allowed to know every agent.

Rules checked by tests: Intake and ModelAccess name no agent, ModelAccess depends on nothing
in the solution, and an agent project references only Intake and ModelAccess.

| Slice | Folder |
|---|---|
| Change request intake | `specs/001-change-request-intake` |
| Approval gate | `specs/002-change-request-approval-gate` |
| Requirements drafting | `specs/003-requirement-drafting` |
| Model-backed draft generator | `specs/004-model-draft-generator` |
| HTTP API | `specs/005-web-api` |
| Organise agent code by agent | `specs/007-organise-by-agent` |
| Architect design drafting | `specs/008-architect-design-drafting` |
| Shared agent plumbing | `specs/009-shared-agent-plumbing` |

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
| POST | `/change-requests/{id}/designs` | Request a design draft for an Approved request that has a requirements draft (calls the model) |
| GET | `/change-requests/{id}/designs/latest` | Latest design draft |
| GET | `/change-requests/{id}/designs` | Design draft version history |

Errors are JSON with a stable `code` and never include stack traces, endpoints or secrets.

## Storage

By default the API keeps everything in one SQLite file at
`<local application data>/AgenticChangeOps/changeops.db` (outside the repository), created on first
use. Two optional settings, as environment variables or configuration:

| Setting | Meaning |
|---|---|
| `ChangeOps__Storage__Path` | Where the database file lives |
| `ChangeOps__Storage__Provider` | `Sqlite` (default) or `InMemory` (lost on restart; what the automated tests use) |

If the file cannot be opened, or was written with a layout this version does not understand, the
API still starts and each operation that needs storage answers `503` with code
`storage-unavailable` (category `Unavailable` or `UnsupportedLayout`); nothing is changed. If two
overlapping requests would take the same version number, one succeeds and the other gets `409`
with code `version-conflict`. Neither response carries a path or any internal detail. Database
files are git-ignored.

## Enabling requirements drafting (optional)

Every operation except drafting works with no cloud account. Without model settings, a draft
request returns `503 drafting-not-configured` and a design request returns `503 design-not-configured`.
Both use the same settings.

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

- Developer and Reviewer agents
- Orchestration between agents (Microsoft Agent Framework)
- MCP integration for external tools
- Authentication

## License

[MIT](LICENSE)
