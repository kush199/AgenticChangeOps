# Research: Model-Backed Requirements Draft Generator

Each decision resolves a planning item deferred by the spec's clarification session.

## 1. Where the production adapter lives

- **Decision**: A new class library `AgenticChangeOps.Intake.AzureOpenAI`; tests in the existing
  test project.
- **Rationale**: Slice 003 deliberately shipped with zero production dependencies and named this
  adapter as a later slice. A separate project preserves that for the core library.
- **Alternatives**: Same project as intake (rejected: forces cloud packages on every consumer);
  separate test project (rejected: two projects to run for one picture; no benefit).

## 2. Model client library

- **Decision**: `Azure.AI.OpenAI` 2.1.0 (stable) with `Azure.Identity`'s default credential
  chain. Verified present on nuget.org at planning time.
- **Rationale**: First-party Azure OpenAI client; supports token credentials, so no key is
  needed (FR-012); supports a structured-JSON response mode and a finish reason, which the size
  rule uses.
- **Alternatives**: Semantic Kernel connector (see plan stack note; swap-in through the seam);
  raw HTTP (rejected: re-implements auth, retries, and error shapes).

## 3. Authentication

- **Decision**: `DefaultAzureCredential` constructed inside the adapter; no key setting exists.
- **Rationale**: Covers a signed-in developer and a managed identity with no configuration, and
  matches the spec ("operator's cloud identity where available").
- **Alternatives**: Key from an environment variable (rejected by spec); a narrower credential
  type (rejected: breaks one of the two target identities).
- **Consequence**: Any identity failure is reported as category `Unreachable` and carries no
  identity detail (spec US4 scenario 3).

## 4. Configuration names and defaults

- **Decision**: Environment variables, validated once at creation:

  | Variable | Required | Default |
  |----------|----------|---------|
  | `AGENTICCHANGEOPS_MODEL_ENDPOINT` | yes | none |
  | `AGENTICCHANGEOPS_MODEL_DEPLOYMENT` | yes | none |
  | `AGENTICCHANGEOPS_MODEL_TIMEOUT_SECONDS` | no | 30 |
  | `AGENTICCHANGEOPS_MODEL_MAX_RESPONSE_CHARS` | no | 32000 |

- **Rationale**: Constitution requires environment variables. Defaults fit "tens of seconds"
  and "a few tens of kilobytes". Names are provider-neutral so the seam can change provider.
- **Alternatives**: `appsettings.json` (rejected: invites committing real values).

## 5. Placeholder rule

- **Decision**: A value is a placeholder if, after trimming, it is empty; wrapped in `<...>` or
  `{...}`; starts with `YOUR_` or `REPLACE` (case-insensitive); or contains `placeholder`
  (case-insensitive). The endpoint must also be an absolute `https` URI. The rule is documented
  next to the sample variable list in the quickstart.
- **Rationale**: Catches the forms documentation actually uses, without trying to guess
  validity of real endpoints.
- **Alternatives**: Allow-list of real endpoint shapes (rejected: brittle across clouds).

## 6. Size limit semantics

- **Decision**: The limit counts characters of the returned text. It is enforced twice: the
  request carries a maximum output token count derived from the limit (limit divided by 3,
  rounded up), and the finished text is checked. A model finish reason of "length", or text over
  the limit, is `ResponseTooLarge`; no part of the answer is used.
- **Rationale**: Bounds cost as well as accepting only complete answers. Characters are what a
  reviewer reasons about; tokens are an implementation detail.
- **Alternatives**: Bytes (rejected: no benefit for text); check only after receipt (rejected:
  pays for a response it will discard).

## 7. Response format and parsing

- **Decision**: The model is asked for a single JSON object with exactly the keys `summary`,
  `functionalRequirements`, `acceptanceCriteria`, `assumptions`, `openQuestions`, using the
  service's JSON response mode. The parser accepts the object alone or inside one code fence
  (clarification 1); any other surrounding text, multiple blocks, a missing key, or a wrong
  shape (string where a list is required, non-string list items) is `MalformedOutput`. Unknown
  keys are ignored. Present-but-empty values pass through for slice 003 validation.
- **Rationale**: JSON is unambiguous to parse with the framework library; the single-fence rule
  is the clarified behaviour. Absent-versus-empty semantics match `DraftContent`.
- **Alternatives**: Markdown headings (rejected: ambiguous parsing); function calling (rejected:
  more provider surface for the same result).

## 8. Prompt construction and injection

- **Decision**: A fixed system instruction states the role, the format, and "use only the data
  provided". The change request's descriptive fields (title, description, affected system,
  priority, change type) are serialised as one JSON object and placed after a clear delimiter in
  the user message. The requester, identifier, status and timestamps are not sent.
- **Rationale**: JSON serialisation escapes quotes and control characters, so field text cannot
  close the data block or forge instructions; excluding the requester minimises personal data
  sent out (FR-004).
- **Alternatives**: Plain interpolation (rejected: injection and format confusion).

## 9. Timeout and cancellation

- **Decision**: The generator links the caller's token with a timeout token. Expiry of the
  timeout is `TimedOut`; cancellation by the caller propagates as cancellation. The SDK retry
  policy is set to zero retries (FR-010).
- **Rationale**: Distinguishes "the model was slow" from "the caller gave up".
- **Alternatives**: Rely on the SDK's own timeout (rejected: harder to test and to name).

## 10. Failure mapping

- **Decision**: HTTP 408 and client timeouts to `TimedOut`; 401, 403, 404, 429, 5xx, network
  and identity failures to `Unreachable`; content-filter blocks, empty text, and unparseable
  text to `MalformedOutput`; finish reason length to `ResponseTooLarge` (clarification 4).
- **Rationale**: Four categories only; each maps to a different caller action.
- **Alternatives**: A fifth "declined" category (rejected in clarification).

## 11. Logging

- **Decision**: None in this slice. The category is returned in the error; elapsed time is not
  recorded.
- **Rationale**: The spec permits category and elapsed time only; the category is already
  visible, and no logging package means nothing to leak through (SC-004).
- **Alternatives**: `ILogger` with a fixed template (deferred until an operator needs timing).

## 12. Live check

- **Decision**: One xUnit test in the existing project, skipped unless
  `AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1` is set in addition to the model settings. The skip
  reason names the variable. It builds the generator from the environment, generates for one
  synthetic change request, and asserts only that parsing succeeded and the five sections are
  present; it prints nothing from the prompt or answer.
- **Rationale**: Visible as an explicit skip in every run, never runs by accident, and reuses
  the same factory path an operator uses.
- **Alternatives**: Separate console tool (rejected: another project, same value); trait filter
  only (rejected: would still run under a bare `dotnet test`).
