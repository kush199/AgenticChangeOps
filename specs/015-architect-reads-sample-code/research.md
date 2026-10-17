# Research: Architect Consults A Sample Codebase

No `NEEDS CLARIFICATION` remains from the plan. Each decision below was either open in the spec
(deferred to planning) or a technology choice.

## R-001 In-process MCP, not a child process or network server

- **Decision**: Run the MCP server and client inside the API process, connected by in-memory
  pipes (the SDK's stream transports). One server instance per design request.
- **Rationale**: "No network or other system is reachable" is easiest to prove when there is no
  socket and no child process. Per-request lifetime means no state survives between requests and
  limits are per request by construction. Still real MCP, so the tools are discovered and called
  through the protocol, as the spec requires.
- **Alternatives**: stdio child process (extra process, launch and path concerns on CI, harder to
  fake); HTTP server (opens a port, against the intent); plain function tools without MCP (fails
  the spec's "through the Model Context Protocol").

## R-002 MCP SDK package

- **Decision**: `ModelContextProtocol` 2.2.0 in the new project only. It has a net8.0 target.
  The server side publishes the tools; the client side lists them and exposes each as an
  `AIFunction`, which the Agent Framework tool loop accepts.
- **Rationale**: Official SDK, matches the constitution's MCP line, no hand-written protocol.
- **Alternatives**: hand-written JSON-RPC (more code to get wrong).
- **To verify in the first task**: the package restores on the CI SDK, its transitive versions do
  not disturb the pinned `OpenAI` and `Azure.AI.OpenAI` set, and in-memory stream transports are
  available. If a transitive conflict appears, pin explicitly and note it in the PR.

## R-003 Tool-calling loop and the model-call limit

- **Decision**: A second model client builds the agent with the session's tools and the
  framework's function-invocation behaviour, with the maximum iterations set to the model-call
  limit. The first model call counts as one.
- **Rationale**: Each loop iteration is one model call, so the cap is exactly "maximum number of
  model calls". The cap is ours; the model cannot raise it.
- **At the cap**: if the cap is reached while the model still wants tools and no final text
  exists, the request fails `LimitsExhausted`. If a valid final text exists, it is used.
- **To verify**: what the framework does when the cap is hit (stops quietly or throws) is pinned
  by a test, and either way is mapped to the above.

## R-004 Where each limit is enforced

| Limit | Enforced by | On breach |
|---|---|---|
| Tool calls per request | `ToolBudget` counts every call, served or refused | Call refused with a fixed message, counted, request continues |
| Model calls per request | Loop iteration cap (R-003) | No valid answer yet: `LimitsExhausted`; valid answer: stored |
| Data returned per request | `ToolBudget` sums bytes returned; a result that would exceed it is refused whole, not cut | Refused with a fixed message, counted |
| Size per file read | `SampleReader` checks length before reading | Refused with a fixed message, counted |
| Time limit per request | One cancellation source for the whole request, linked to the caller's token, passed to the loop and each tool | `LimitsExhausted` when no valid answer exists; the existing per-model-call timeout keeps its own category |
| Retries | Nothing retries: the client retry policy is already 0; tools never re-run a failed call | n/a |

The existing per-call `RequestTimeout` and `MaxResponseCharacters` are unchanged and still apply
to each model call.

## R-005 Safe defaults (the values the spec deferred)

| Setting | Default | Reason |
|---|---|---|
| Tools switched on | off | Spec: off by default |
| Max tool calls | 8 | Enough to list, read three or four files and search a couple of times |
| Max model calls | 6 | Bounds a refusal loop |
| Max data returned | 24,000 bytes | A few small files |
| Max size per file read | 8,000 bytes | The largest invented file is far smaller |
| Time limit | 60 seconds | Twice the single-call default |

Absent values take the default. Present but invalid values (zero, negative, non-numeric) make
design drafting report not configured, with the setting name only in the log, following the
existing settings behaviour. A tool-call limit of zero is therefore invalid rather than a special
state; this refines the spec edge case about zero limits.

## R-006 Confinement

- **Decision**: Accept only relative paths of plain segments. Reject empty, absolute,
  drive-qualified, UNC, rooted, parent (`..`), control-character, colon and over-long paths
  *before* touching the disk, accepting `/` and treating `\` as invalid. Then combine with the
  sample root, resolve links, and require the final full path to be inside the real sample root.
  A link whose target is outside is refused.
- **Rationale**: Two independent layers (textual, then resolved), so one mistake does not open
  the sandbox. Works on Windows and Linux.
- **Test set**: at least 20 escaping spellings (SC-003), run on both CI platforms.
- **Message hygiene**: refusal text is fixed per reason and never echoes the supplied path or any
  real path.

## R-007 Untrusted tool output

- **Decision**: Tool results travel only as tool-result content in the model conversation. The
  system text is built before any tool runs and never rebuilt from results. The tool list is
  fixed at session creation. File text is returned under a fixed first line naming the relative
  path and saying it is data.
- **Residual risk**: a model can still be persuaded by text in a file, but its only possible
  effect is the content of the design draft, which passes the existing validator and the
  human-reviewed workflow. No status, approval or routing can be influenced. Stated in the README.

## R-008 Files relied on

- **Decision**: The list is the de-duplicated set of relative paths of files successfully read
  (served reads, not refused), in first-read order. Listing and search results do not count as
  "read". The session computes it from its own records, not from model text, so the model cannot
  claim a file it did not read.
- **Storage**: `DesignDraft.SampleFilesRead`, `IReadOnlyList<string>?`, null when tools were off.
  Serialized only when not null (this also corrects the spec's earlier "empty list for old
  drafts", which would have changed tools-off output).

## R-009 Audit of tool calls

- **Decision**: One audit entry per call, event type `ToolCalled`, with typed members: tool name,
  target (path or search term), outcome, bytes returned, elapsed milliseconds. Never content.
  Entries are written under the store lock immediately before the design and its
  `DesignGenerated` entry, in call order.
- **Search terms** are model-produced text; they are truncated and stripped of control characters
  before audit.
- **Known consequence**: the spec says a failed request stores nothing, so the calls of a request
  that ends in `ToolsUnavailable` or `LimitsExhausted` are not audited. Counts only (calls made,
  refusals by outcome name, elapsed time) appear in a log line, with no content, paths or terms.
  Decided (see spec Clarifications, 2026-10-17).
- **Alternative rejected**: a new store method for atomic multi-entry saves. The spec rules out
  storage changes beyond one added field, and a crash between the tool entries and the design only
  leaves audit entries for calls that really happened.

## R-010 Failure categories and the HTTP surface

- **Decision**: Append `ToolsUnavailable` and `LimitsExhausted` to `GenerationFailureCategory`.
  The existing design mapping returns 502 `generation-failed` with `Category` set to the name, so
  the two new values need no new status code, error code or body shape.
- **When `ToolsUnavailable`**: the session factory cannot start (sample folder missing, empty or
  unreadable, tool server does not start, or its tool list is not exactly the three expected
  tools). Decided before any model call.
- **Automatic advancement** already records a failed attempt by category; a test confirms the new
  categories need no change there.

## R-011 Delivering the sample at run time

- **Decision**: The Api project copies `sample-codebase/` to its output folder as content. The
  default root is `<app folder>/sample-codebase`; one optional setting overrides it. Tests use
  temporary folders.
- **Rationale**: The sample stays outside `src/` and the solution, yet a published app has it.
- **Alternative rejected**: locating the repository root at run time (fails when published).

## R-012 Tools-off identity

- **Decision**: With the switch off, no session factory is registered, the generator takes its
  existing code path, the prompt text is the existing text, and the new members are null and
  omitted from JSON. The unchanged existing suite is run, plus a byte comparison of a design
  response and a stored design payload against recordings taken before the change.

## Verified at T004 (spike, 2026-10-17, not committed)

- **R-002 (a) restore**: `ModelContextProtocol` 2.2.0 restores for net8.0 next to the existing
  packages with no conflict. Resolved versions stay `OpenAI` 2.14.0, `Azure.AI.OpenAI`
  2.9.0-beta.1, `Microsoft.Agents.AI` 1.24.0, `Microsoft.Extensions.AI` 10.10.0 and
  `Microsoft.Extensions.AI.Abstractions` 10.10.1; the SDK adds `ModelContextProtocol.Core` 2.2.0.
- **R-002 (b) in-memory pair**: `McpServer.Create(new StreamServerTransport(...))` and
  `McpClient.CreateAsync(new StreamClientTransport(...))` over two `System.IO.Pipelines.Pipe`
  instances work in one process; `client.ListToolsAsync()` returns tools that are `AIFunction`s
  and invoke correctly. No socket and no child process.
- **R-003 (c) loop cap**: with `FunctionInvokingChatClient.MaximumIterationsPerRequest = N`, a
  model that always asks for a tool is called **N + 1** times (the first call is not counted as an
  iteration), and when the cap is hit the loop returns quietly with **empty text and no
  exception**. A cap of 0 is rejected (the builder throws), so the framework cap alone cannot
  express "at most 1 model call".
- **Consequence (replaces the R-003 mechanism)**: the model-call limit is enforced exactly by our
  own counting wrapper around the inner `IChatClient`. The (limit + 1)th model call is refused
  inside the wrapper and surfaces as `LimitsExhausted`; the framework cap is set to the limit as a
  backstop only. A valid final text on call number `limit` is returned normally. Empty text with
  an unanswered tool call is also treated as `LimitsExhausted`.
