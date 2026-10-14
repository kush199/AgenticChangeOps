# Contract: Wire parity between the two clients

Both clients are run against the same recording transport with the same inputs and compared.

## Must be identical

- Number of requests (1)
- HTTP method and operation (chat completions for the same deployment)
- `messages`: count, order, roles, and content text
- `response_format`: JSON object
- Absence of `tools`, `tool_choice`, `functions`
- Absence of any prior-turn content on a second request
- Value of the output-token cap

## Allowed to differ (each listed in comparison.md with the observed value)

- Name of the output-token cap field on the wire
- Client identification headers (user agent string)
- Optional per-message author name attribute, if the framework sets one
- Any field the framework adds that does not alter the model's instructions or the requested structure (must be listed; if it could alter them, it is a failure under FR-009)

Anything else that differs is a failure to be fixed or recorded for the user.
