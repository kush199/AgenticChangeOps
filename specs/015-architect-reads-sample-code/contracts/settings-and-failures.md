# Contract: Operator Settings And Failure Categories

Read once at startup, like the other settings. Environment variable or configuration key; the
names below are the environment form. Errors name the setting, never the value.

| Setting | Default | Valid |
|---|---|---|
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED` | `false` | `true` or `false` only; anything else is invalid |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_CALLS` | 8 | positive whole number |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_MODEL_CALLS` | 6 | positive whole number |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_RETURNED_BYTES` | 24000 | positive whole number |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_MAX_FILE_BYTES` | 8000 | positive whole number |
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_TIME_LIMIT_SECONDS` | 60 | positive whole number |
| `AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH` | `<app folder>/sample-codebase` | existing folder, checked when a session opens |

When the switch is off, no other tools setting is parsed or validated, so a bad tools setting
cannot affect a tools-off server. An invalid setting while the switch is on makes design drafting
report not configured (the existing `design-not-configured` 503), with a name-only warning in the
log. An invalid switch value counts as on-but-invalid, not as off.

## Failure categories (design route)

| Category | When | Stored? | HTTP |
|---|---|---|---|
| `ToolsUnavailable` | Switch on, but the sample folder or tool server cannot be made ready; decided before any model call | nothing | 502 `generation-failed`, `Category: ToolsUnavailable` |
| `LimitsExhausted` | A limit (model calls or time) ends the request before a valid answer exists | nothing | 502 `generation-failed`, `Category: LimitsExhausted` |

Existing categories are unchanged. The body and message for the two new categories use the same
shape as today ("The design draft could not be generated."); no new field, header or status code.

## Never appears in any error, log or response

File content, model text, endpoint, deployment name, credential, or any filesystem path outside the
sample. Logs may carry counts only (calls made, refusals by outcome name, elapsed time).
