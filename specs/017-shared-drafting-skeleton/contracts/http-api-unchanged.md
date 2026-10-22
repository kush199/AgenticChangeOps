# Contract: HTTP Interface Unchanged

Before any code moves, record (as golden files under `tests/AgenticChangeOps.Api.Tests/Baseline/`) the status code, asserted headers, body and error code for every drafting operation of the three agents: produce, latest, history; success; unknown id; ineligible status; missing requirements draft; missing or outdated design draft; not configured; every generator failure category; unusable content; each Developer rejection reason. After the extraction the same operations are repeated and compared byte for byte. `openapi.json` and existing goldens must show no diff.

Leak scans stay: no model text, endpoint, deployment name or credential in responses, logs or errors.
