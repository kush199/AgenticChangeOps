# Contract: HTTP surface unchanged

Recorded before any code moves (using the existing model-backed baseline tests and fake client), compared after.

| Operation | Cases recorded |
|---|---|
| Request a design | success; each failure category (Unreachable, TimedOut, MalformedOutput, ResponseTooLarge); not configured; ineligible; unknown id; caller cancellation |
| Get a design | present; absent; unknown id |
| Workflow status and advance | stages that reach the design step |

Compared: status code, body, headers, error code. The recorded set is the existing `SharedPlumbingBaselineTests` and `CallerCancellationBaselineTests` goldens; they must pass with assertions unchanged. The framework path itself is exercised by the wire tests, not by HTTP tests (HTTP tests use fakes by design).
