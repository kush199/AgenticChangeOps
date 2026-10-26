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
| **Automatic advancement** | Optional and off by default: when switched on, runs the requirements and design draft steps for requests that have one due, without anyone asking. Never makes a decision |
| **Change proposals** | Optional, needs the sample tools: a Developer agent proposes a text-only change set (quoted edits, risks, test ideas) for an approved request with a current design. Every quote is checked against the real sample file; nothing is ever applied. Versioned and audited as `ProposalGenerated` |
| **Reviews** | A Reviewer agent writes an advisory review of the latest change proposal of an approved request. The system first re-checks every proposed edit against the sample on its own; the model only writes findings, each citing a real entry of the requirements, design or proposal. Versioned and audited as `ReviewGenerated`; it **never changes any status and decides nothing** |
| **HTTP API** | All of the above, over a local JSON API with a Swagger page. Reviews can be requested over HTTP; reading them back is internal only for now |

Not built yet: HTTP operations to read reviews back, a human acceptance step for reviews, applying a proposal,
orchestration between agents, authentication.

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
  AgenticChangeOps.Drafting/            agent-neutral drafting skeleton: the order of steps every drafting service follows, plus latest and history reads
  AgenticChangeOps.Agents.Requirement/  everything specific to the Requirement agent
  AgenticChangeOps.Agents.Architect/    everything specific to the Architect agent
  AgenticChangeOps.Agents.Developer/    everything specific to the Developer agent (text-only change proposals)
  AgenticChangeOps.Agents.Reviewer/     everything specific to the Reviewer agent (advisory reviews of a proposal; no tools)
  AgenticChangeOps.SampleCode/          the read-only sample codebase tools (in-process MCP server), used by the Architect and Developer
tests/
  AgenticChangeOps.Api.Tests/                  HTTP behaviour, recorded baseline of responses
  AgenticChangeOps.Intake.Tests/               intake and approval, no agent or model references
  AgenticChangeOps.Persistence.Tests/          one store contract run against both stores, restart, atomicity, layout
  AgenticChangeOps.ModelAccess.Tests/          settings, failure mapping, hygiene and neutrality checks
  AgenticChangeOps.Drafting.Tests/             the skeleton's own behaviours, neutrality checks, a stand-in fourth agent
  AgenticChangeOps.Agents.Requirement.Tests/   drafting, validation, prompt, parser, generator
  AgenticChangeOps.Agents.Architect.Tests/     design drafting, validation, prompt, parser, generator
  AgenticChangeOps.Agents.Developer.Tests/     proposal verification, validation, prompt, parser, generator, storage round trip
  AgenticChangeOps.Agents.Reviewer.Tests/      system check, references, validation, prompt, parser, generator, service, storage round trip, structure
  AgenticChangeOps.SampleCode.Tests/           path confinement, limits, tools, MCP round trip, sample hygiene
sample-codebase/                        invented sample the tools read (not part of the solution)
specs/                                  one folder per slice: spec, plan, tasks, contracts
.specify/                               Spec Kit templates, scripts and the constitution
```

### Adding an agent

Each agent is one project beside the Requirement agent. Adding one needs no edit to the
Requirement agent or to ModelAccess:

1. Create `src/AgenticChangeOps.Agents.<Name>`, referencing `AgenticChangeOps.Intake`,
   `AgenticChangeOps.ModelAccess` and `AgenticChangeOps.Drafting` (never another agent). Put the agent's records, validation,
   service, interface, prompt, section names and generator and its output type (implementing
   `IAgentOutput`) there. Use `ModelCaller`, `ModelAnswerReader` and `SectionNormaliser` from
   ModelAccess for the model call, answer reading and list/text limits instead of copying them, and a
   `DraftingEngine` from Drafting for the sequence, with the agent's own `IDraftingAgent` and save step.
2. Create `tests/AgenticChangeOps.Agents.<Name>.Tests` and add both projects to the solution.
3. Reference the new project from `AgenticChangeOps.Api` and register its services in
   `Composition/ApiServiceRegistration.cs`, the one place allowed to know every agent.

Rules checked by tests: Intake and ModelAccess name no agent, ModelAccess depends on nothing
in the solution, and an agent project references only Intake, ModelAccess and Drafting, and Drafting names no agent.

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
| GET | `/operator/automatic-advancement` | Whether automatic advancement is on, and the latest check summary |

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
with code `version-conflict`. Neither response carries a path or any internal detail. A file written
by the previous layout (version 1) is upgraded in place on first use. Database
files are git-ignored.

## Enabling requirements drafting (optional)

Every operation except drafting works with no cloud account. Without model settings, a draft
request for an existing change request returns `503 drafting-not-configured` and a design request
returns `503 design-not-configured`; an unknown identifier still returns `404 not-found`.
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

## Automatic advancement (optional, off by default)

When switched on, the system checks the stored change requests every interval and runs the next
step for any request that has one due, without anyone calling advance. Only the two draft steps
run unasked: a **requirements draft** for a new request (or an Approved one that has none) and a **design draft** for an Approved
request that has a requirements draft. Both only produce draft content: they never change a
status, record a decision or clear anything, which is the only reason they may run without a human
asking. **No decision is ever made automatically.** What is due is decided by the same rules as the
workflow status, never by a language model, and the step is the same one a manual advance runs. At
most one workflow step runs for a request at a time, whether started by the system or a person.

| Setting (environment variable) | Default | Meaning |
|---|---|---|
| `ChangeOps__AutoAdvance__Enabled` | `false` | Switches the feature on (only an explicit `true` does) |
| `ChangeOps__AutoAdvance__IntervalSeconds` | `300` | Time between checks (1 to 86400) |
| `ChangeOps__AutoAdvance__MaxStepsPerCheck` | `5` | Most steps one check may run or attempt (1 to 1000) |
| `ChangeOps__AutoAdvance__FailureWaitSeconds` | `900` | Wait before a failed request is tried again (0 to 86400) |
| `ChangeOps__AutoAdvance__MaxConsecutiveFailures` | `3` | Failures after which the request is left for a person (1 to 100) |

Settings are read at start-up (switching off needs a restart). An invalid value uses that
setting's default and logs a warning that names the setting, never its value. If the model is not
configured, no step runs and nothing is recorded as a failure.

A failed attempt stores no draft, is not retried until the waiting time has passed, and after the
configured number of consecutive failures the request is left for a person: its workflow status
shows `"automatic": { "mode": "LeftForPerson", "failureCategory": "..." }` and the system stops
trying until a person's advance succeeds or a different step becomes due. Workflow status and
advance responses gain an `automatic` member (`Off`, `On` or `LeftForPerson`); an audit entry of a
step run automatically carries `"automatic": true` (manual entries do not have the member).

`GET /operator/automatic-advancement` shows whether the feature is on and the latest check:

```json
{ "enabled": true, "lastCheck": { "checkedAt": "2026-10-16T09:30:00Z", "examined": 12, "ran": 2, "failed": 1 } }
```

`lastCheck` is `null` until a check has run since start. Counts and times only: no model text,
endpoint, deployment name or credential. Each check is also logged as one line, for example
`Automatic advancement check: examined=12 ran=2 failed=2 categories=TimedOut:1,MalformedOutput:1`.
A person calling an agent's own drafting operation directly is outside the workflow step and takes
no part in the one-step-at-a-time rule.

## Sample codebase tools (optional, off by default)

When switched on, the Architect agent may look at a small **invented** sample codebase while it
prepares a design draft, so the design can refer to files that really exist. The tools are offered
through the Model Context Protocol by an in-process server (no process, no network): `list_files`,
`read_file` and `search_text`. They are read-only, confined to the `sample-codebase/` folder, and
reading is classified as non-consequential. The model still only generates content: nothing it reads
can change a status, record a decision or add a tool, and eligibility, routing and approval stay the
system's own rules.

The sample (`sample-codebase/`, outside the solution and never compiled) holds two invented systems,
an orders service and a billing portal, each with settings, a few pseudo-source files and short
design notes. It is copied next to the binaries when the API is built.

| Setting (environment variable) | Default | Meaning |
|---|---|---|
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED` | `false` | Switches the feature on (`true` or `false` only; anything else is invalid) |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS` | `8` | Most tool calls per design request |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_MODEL_CALLS` | `6` | Most model calls per design request |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_RETURNED_BYTES` | `24000` | Most data returned to the model per request |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_FILE_BYTES` | `8000` | Largest file one read may return |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_TIME_LIMIT_SECONDS` | `60` | Time limit for the whole request |
| `AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH` | `<app folder>/sample-codebase` | Optional: another folder to read |

While the switch is off nothing changes: no other tools setting is read and every response is
exactly as before. With it on and a setting invalid, design drafting reports
`503 design-not-configured` (only the setting name is logged).

A design drafted with tools carries `sampleFilesRead`: the sample files the Architect actually read
(empty if it read none). Each tool call is recorded in the audit trail as a `ToolCalled` entry (tool,
path or search term, outcome, bytes returned, milliseconds) and never the file text. A path outside
the sample, an oversized file or a call over a limit is refused with a fixed message and the design
carries on within the limits. A request that cannot start the tools, or runs out of limits before a
usable answer, fails with `502 generation-failed` and `category` `ToolsUnavailable` or
`LimitsExhausted`, and stores nothing (its calls are not audited; only counts are logged).

Limits of the idea: a model can still be persuaded by text in a file, but the only thing that can
change is the wording of the draft, which passes the usual validation and is reviewed by a person.

## Developer agent: change proposals (optional, needs the sample tools)

When the sample codebase tools above are switched on, the Developer agent can propose a **change
set as text** for an approved change request that has a requirements draft and a design draft based
on the latest requirements draft. It reads the sample through the same read-only tools and returns a
summary, a list of proposed edits, risks, ideas for testing, open questions and the sample files it
read. Each edit names a file inside the sample, says whether it changes an existing file or adds a
new one, quotes the exact existing text it replaces (for a change) and gives the replacement text.

**It never applies anything.** Nothing is created, changed, deleted, compiled or run, and every
proposal states that it has not been compiled, run or tested. Producing a proposal is classified as
non-consequential for that reason; applying one would be consequential and is not built. A proposal
never changes a change request's status, approves nothing and clears nothing.

The system checks every edit against the real sample file before storing anything: the quote must
appear exactly once (line endings do not matter, every other character does), a file proposed as new
must not exist, every path must stay inside the sample, and the proposal must stay within a maximum
number of changes and a maximum total size. One bad edit rejects the whole proposal and nothing is
stored.

| Operation | Route |
|---|---|
| Request a proposal | `POST /change-requests/{id}/proposals` |
| Latest proposal | `GET /change-requests/{id}/proposals/latest` |
| Proposal history (version order) | `GET /change-requests/{id}/proposals` |

Requesting again creates a new version and keeps earlier ones. Errors follow the design operations:
`400 invalid-identifier`, `404 not-found` / `no-proposal`, `409 conflict` (not Approved, no
requirements draft, no design draft, or the design is not based on the latest requirements draft,
also re-checked just before storing), `503 proposal-tools-unavailable` (the tools switch is off) or
`proposal-not-configured`, `502 generation-failed` with a `category`, and `502 proposal-rejected`
with a `category` naming only the reason (for example `QuoteNotFound`). No file text, model text,
endpoint, deployment name or credential appears in any error, log or response.

| Setting (environment variable) | Default | Meaning |
|---|---|---|
| `AGENTICCHANGEOPS_PROPOSAL_MAX_CHANGES` | `5` | Most edits in one proposal |
| `AGENTICCHANGEOPS_PROPOSAL_MAX_BYTES` | `6000` | Most bytes of quoted plus replacement text |

These are read only when `AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED=true`; the tool and model limits above
apply to each proposal request. Each stored proposal is audited with one `ProposalGenerated` entry,
after one `ToolCalled` entry per tool call (never with file text). Once a current design exists and
proposals are available, the workflow status shows `offeredStep: "Proposal"` next to its unchanged
stage and next step; automatic advancement never runs a proposal, it runs only when a person asks.
With the Developer unused, every existing response is exactly as before.

## Reviewer agent: advisory reviews (one HTTP operation to request)

For an **Approved** change request that has a requirements draft, a design draft based on the latest
requirements draft, and a change proposal based on the latest design draft, the Reviewer agent can write
an **advisory review** of the proposal. It is requested over HTTP and read back through the service
(`IReviewService`: latest review, review history in version order), which is obtained from the host's
service provider; there is no HTTP operation to read reviews yet.

| Operation | Route |
|---|---|
| Request a review (no body) | `POST /change-requests/{id}/reviews` |

Success is `201` with the stored review and no link. Checks run in this order: malformed id `400`
`invalid-identifier`, unknown id `404` `not-found`, reviews not configured `503` `review-not-configured`,
then the review itself: not eligible or outdated is `409` `conflict` naming the rule, a model failure is
`502` `generation-failed` and refused findings are `502` `review-rejected`, each carrying only a category.

A review has two parts. The first is checked by the **system**, without any model: every proposed
edit's quoted text is re-checked against the current sample, and only the edit's position and a fixed
outcome (found, quote not found, ambiguous, file not found, outside the sample, new file free or already
existing, malformed) is recorded. If the sample is not available the check is recorded as **not
performed** and the review carries on. The model never writes that part and cannot alter it. The second
part is written by the model: findings about whether the proposal meets the acceptance criteria, is
consistent with the design, and leaves risks unmentioned, plus open questions. Each finding has a
category, an **advisory** severity, a plain-language statement and at least one reference of the form
`document.list[position]` (for example `proposal.changes[2]`) to an entry that really exists in the
records the review is based on. One finding whose reference points to nothing, a review over the maximum
number of findings or the maximum size, or an unusable model answer rejects the **whole** review and
nothing is stored.

**It decides nothing.** A review never changes a status, approves, rejects or clears anything, and
nothing in the system reads its severity; it is classified as non-consequential content. The Reviewer
agent uses **no tools** and reads nothing itself: the system's own re-check is the only place the sample
is read, and it only reads. If the requirements, design or proposal stop being the latest while a review
is being prepared, the review is refused as outdated and nothing is stored.

Requesting again creates a new version and keeps earlier ones. Each stored review is linked to the
versions of the requirements draft, design draft and proposal it was based on, is a `Draft`, and is
audited with one `ReviewGenerated` entry (no model text, file content or path). Errors carry a specific
reason or only a failure category; no model text, file content, endpoint, deployment name or credential
appears in any error, log or response.

| Setting (environment variable) | Default | Meaning |
|---|---|---|
| `AGENTICCHANGEOPS_REVIEW_MAX_FINDINGS` | `8` | Most findings in one review |
| `AGENTICCHANGEOPS_REVIEW_MAX_BYTES` | `8000` | Most bytes of finding statements plus open questions |
| `AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH` | `sample-codebase/` beside the host | Where the system's own re-check looks |

Reviews use the same model settings as the other agents and **do not need the sample tools switched
on**. The settings are read only the first time the review service is used; a bad value (or missing
model settings) makes reviews "not configured", which fails before any model call. With the Reviewer
unused, every existing response is exactly as before.

## Roadmap

- HTTP operations to read reviews back, a human acceptance step for reviews, using the constitution's principles as review input, and a human-approved way to apply a proposal
- Orchestration between agents (Microsoft Agent Framework)
- MCP tools for real systems (the Architect and Developer have read-only sample tools today)
- Authentication

## License

[MIT](LICENSE)
