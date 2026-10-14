# Contract: Dependency and neutrality rules

| # | Rule | Check |
|---|---|---|
| 1 | `AgenticChangeOps.Workflow` references only Intake, Agents.Requirement, Agents.Architect, ModelAccess | project-reference test in `Workflow.Tests` |
| 2 | No agent project, Intake, or ModelAccess references Workflow | same test, reversed direction |
| 3 | The two agent projects still do not reference each other | existing agent structure tests, unchanged |
| 4 | `Api` references Workflow; Workflow does not reference Api or ASP.NET | project-reference test |
| 5 | Workflow source names no endpoint, route, HTTP type, model, prompt, endpoint setting or credential | source scan test (same style as the ModelAccess neutrality scan) |
| 6 | Workflow never constructs a draft, audit entry or decision | source scan: no `new RequirementsDraft`, `new DesignDraft`, `new ChangeRequestAuditEntry`, no `Approve`/`Reject`/`Defer` call, no `SaveAgentOutput`/`SaveDraft`/`SaveDesign` call |
| 7 | Workflow writes to the store only through the agents' own services | rule 6 plus a service test asserting store contents after each outcome |
| 8 | Step order is decided only by `WorkflowRules` | test: the rules file has no dependency on any generator or service type |
| 9 | Drift guard: a requirements step is offered iff the Requirement agent would accept a draft for that status | test across every `ChangeRequestStatus` using the real agent service with a fake generator |
