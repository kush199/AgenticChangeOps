# AgenticChangeOps Constitution

## Core Principles

### I. Synthetic Data Only

This project is a **public portfolio** repository. Therefore:

- All sample change requests, test fixtures, seed data, and demo scenarios MUST use fully
  synthetic, fictional content. No corporate names, client names, real project identifiers,
  proprietary business logic, or personally identifiable information may appear anywhere in
  the repository.
- Credentials, API keys, connection strings, tokens, and secrets MUST NEVER be committed —
  not in source, not in test fixtures, not in git history. Use environment variables or a
  secrets manager exclusively.
- If a contributor suspects real data has been introduced, they MUST raise it immediately and
  the offending commit MUST be purged from history before any further merges proceed.

**Rationale**: Public visibility means any committed secret or proprietary material is
permanently exposed. Synthetic data protects both contributors and any organisations whose
change-management patterns might inadvertently appear in examples.

### II. Deterministic Routing

- Agent routing logic, state-machine transitions, and orchestration decisions MUST be
  expressed as deterministic C# code — not as LLM-generated output that is then executed.
- LLMs are permitted only to **generate content** (text, structured artefacts, analysis) that
  is subsequently reviewed or persisted; they MUST NOT decide which agent runs next, which
  approval gate is triggered, or whether a state transition occurs.
- Any routing or transition rule that cannot be expressed as a deterministic predicate MUST be
  escalated to a Human Approval Gate (see Principle III) rather than delegated to an LLM.

**Rationale**: Deterministic routing makes system behaviour auditable, testable, and free of
non-deterministic failures. LLMs are powerful content generators but unreliable control-flow
engines.

### III. Human Approval Gates

- Every consequential action — including but not limited to: committing a generated artefact,
  triggering an external system, advancing a change request past a review checkpoint, or
  deploying any output — MUST require an explicit, recorded human approval before execution.
- Approval events MUST be persisted as typed audit records (see Principle IV) with: approver
  identity, timestamp, the specific action approved, and the artefact version at time of
  approval.
- Automated approval or auto-proceed behaviour is prohibited for consequential actions, even
  in CI pipelines, unless the action is explicitly classified as non-consequential in the
  feature spec and that classification is reviewed during the compliance check.

**Rationale**: Enterprise change management demands traceable human accountability. Gates
ensure the system augments human judgment rather than replacing it.

### IV. Typed Auditable Records

- All agent inputs and outputs MUST be represented as strongly-typed C# records or classes —
  no untyped `object`, `dynamic`, or raw `string` blobs crossing agent boundaries.
- Every typed record MUST be persisted and associated with its originating change request ID,
  enabling full reconstruction of the processing chain for any change request.
- Records MUST include at minimum: `ChangeRequestId`, `AgentName`, `Timestamp`,
  `InputPayload`, `OutputPayload`, and `ApprovalStatus` where applicable.
- Schema changes to persisted record types are breaking changes and MUST follow the amendment
  procedure in the Governance section. Purely additive changes (new optional/nullable members
  defaulting to null, or new enum values appended at the end) that leave every existing
  construction, persisted entry, and serialized value valid are non-breaking and only require
  the change to be recorded in the owning slice's plan (Complexity Tracking).

**Rationale**: Auditability is a first-class requirement for enterprise change management.
Typed records make the system inspectable, testable, and refactorable without silent data
loss.

### V. Spec-Driven Slice by Slice

- No application code, infrastructure code, or test code for a feature slice MAY be written
  until that slice has an approved spec (`spec.md`), an approved plan (`plan.md`), and
  approved tasks (`tasks.md`).
- "Approved" means the human collaborator has explicitly confirmed the artefact — a draft or
  AI-generated artefact does not qualify as approved.
- Slice boundaries MUST be kept small enough that the full spec/plan/tasks for a slice can
  be reviewed in a single sitting. If a slice grows beyond that, it MUST be split.
- Constitution compliance MUST be checked for every spec, plan, and PR before approval
  (see Governance).

**Rationale**: Spec-first discipline prevents wasted implementation effort, keeps AI-generated
work aligned with intent, and ensures every piece of code has a traceable design rationale.

### VI. Test-First

- All deterministic logic (routing, state transitions, record validation, approval gate
  evaluation) MUST have tests written **and approved** before implementation begins.
  Red-Green-Refactor is the required cycle.
- All LLM interactions MUST be hidden behind interfaces. Production code MUST NOT call an LLM
  client directly; unit and integration tests MUST use fakes/stubs implementing those
  interfaces — never live LLM calls in the test suite.
- Test coverage for deterministic paths is non-negotiable; PRs that reduce deterministic-path
  coverage MUST be rejected.

**Rationale**: LLM calls are non-deterministic and expensive. Interfaces + fakes make the
deterministic core fully testable in isolation, while real LLM integration is validated
separately in acceptance/smoke tests.

### VII. Simplicity

- Version 1 of AgenticChangeOps MUST contain exactly **four agents**:
  - **Requirement/Product Agent** — captures and refines change requirements
  - **Architect Agent** — produces design and impact analysis
  - **Developer Agent** — generates implementation artefacts
  - **Reviewer/Critic Agent** — evaluates artefacts against requirements and principles
- Adding a fifth (or further) agent requires a written justification document explaining why
  the problem cannot be solved within the existing four-agent topology. That justification
  MUST be approved before any new agent is implemented.
- Framework abstractions, shared utilities, and helper classes MUST only be introduced when
  used by at least two existing callers. No speculative generalization.

**Rationale**: Complex multi-agent topologies are hard to reason about, test, and maintain.
Four well-scoped agents cover the full change-management lifecycle; additional agents
introduce coordination overhead that must be explicitly justified.

## Technology Stack

This section records binding technology choices for V1. Deviations require a constitution
amendment.

- **Language**: C# (.NET 8 or later)
- **Agent Framework**: Microsoft Agent Framework (Semantic Kernel Agent Framework)
- **LLM Provider**: Azure OpenAI Service
- **Tool/Resource Protocol**: Model Context Protocol (MCP) for external tool integrations
- **Persistence**: Local or Azure-hosted storage; schema defined per typed record type
- **Test Framework**: xUnit with fakes for LLM interfaces; no mocking of database in
  integration tests unless explicitly justified per-slice

All LLM configuration (endpoint URLs, deployment names, API keys) MUST be sourced from
environment variables or Azure Key Vault. No hardcoded values.

## Development Workflow

1. **Spec** — author and gain approval for `spec.md` (`/speckit-specify`)
2. **Plan** — author and gain approval for `plan.md` (`/speckit-plan`)
3. **Tasks** — generate and gain approval for `tasks.md` (`/speckit-tasks`)
4. **Constitution Compliance Check** — verify spec, plan, and tasks against this constitution
   before writing any code
5. **Test-First Implementation** — write failing tests, gain approval, then implement
   (`/speckit-implement`)
6. **Human Approval Gate** — all consequential outputs reviewed before advancing
7. **PR Compliance Review** — every PR is checked against this constitution before merge

Branch naming: `feature/<slice-name>`, `fix/<issue>`, `docs/<topic>`.
Commit messages MUST reference the change request ID or slice name.

## Governance

- This constitution supersedes all other development guidelines, README instructions, and
  verbal agreements. In case of conflict, the constitution prevails.
- **Amendment procedure**:
  1. Open a PR with the proposed change to `constitution.md`.
  2. Include a written rationale explaining what changed, why, and the impact on existing
     slices or in-progress work.
  3. Increment `CONSTITUTION_VERSION` according to semantic versioning:
     - **MAJOR** — removal or redefinition of an existing principle, or breaking change to
       governance rules.
     - **MINOR** — addition of a new principle or materially expanded guidance.
     - **PATCH** — clarification, wording improvement, or non-semantic refinement.
  4. The PR MUST pass a constitution compliance review before merge.
- **Compliance review**: Every spec, plan, tasks document, and PR MUST include a checklist
  item confirming it has been reviewed against the current constitution version. Reviews that
  find a violation MUST block the work item until resolved — they MUST NOT be bypassed.
- This constitution does not expire, but SHOULD be reviewed whenever a major new capability
  or agent is proposed.

**Version**: 1.0.1 | **Ratified**: 2026-10-10 | **Last Amended**: 2026-10-11
