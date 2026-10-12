# Quickstart: Validating the HTTP API

Prerequisites: .NET 8 SDK or later. No cloud account is needed for steps 1 to 4.

## 1. Automated suite (no model, no network, no configuration)

```powershell
dotnet test
```

Expected: all tests pass, including slices 001 to 004 unchanged. The API tests host the service
in-process and use a fake generator; no port is opened and no model is called.

## 2. Run the API with drafting not configured

```powershell
dotnet run --project src/AgenticChangeOps.Api
```

Expected: it starts and listens on `http://127.0.0.1:5080` only. Open `/swagger` to try each
operation by hand. Submit a change request, retrieve it, list its audit history, record a
decision and check clearance: all work. Request a draft: the response is 503 with code
`drafting-not-configured` and no setting value.

## 3. Error shapes by hand

Using the browsable page or any HTTP client:

| Try | Expect |
|-----|--------|
| `GET /change-requests/not-a-guid` | 400 `invalid-identifier` |
| `GET /change-requests/00000000-0000-0000-0000-000000000001` | 404 `not-found` |
| Submit with an empty title and bad priority | 400 `validation-failed` naming both fields |
| Send `{` as the body | 400 `invalid-body` |
| Decide twice on an approved request | 409 `conflict` with the specific reason |
| `GET /nope` | 404 `route-not-found`, JSON |

## 4. Leak check

The no-leak test sets sentinel values for the model endpoint and deployment, drives every
route including failure paths, and scans response bodies, the published description, and
captured logs for the sentinels.

```powershell
dotnet test --filter "FullyQualifiedName~NoLeak"
```

Expected: pass, with zero occurrences.

## 5. Optional live drafting (manual, your own signed-in identity)

Set these in your shell session only; never commit them:

| Variable | Value |
|----------|-------|
| `AGENTICCHANGEOPS_MODEL_ENDPOINT` | `<your-resource-endpoint>` |
| `AGENTICCHANGEOPS_MODEL_DEPLOYMENT` | `<your-deployment-name>` |

```powershell
dotnet run --project src/AgenticChangeOps.Api
```

Submit a synthetic change request and request a draft. Expected: 201 with a version 1 draft;
request again for version 2; retrieve latest and history. A failure returns 502 with only a
`category`.

## 6. Exposing beyond the local machine

Only by explicit operator choice, and two settings are needed. Set `ASPNETCORE_URLS` (for
example `http://0.0.0.0:6000`) to listen beyond loopback, and set `ChangeOps__AllowedHosts` to
the host names clients will use (or `*`): by default the API refuses any `Host` header other
than `localhost`, `127.0.0.1` and `[::1]` with 400 `invalid-host`. There is no authentication,
so do not do this on a shared network.

See [contracts/http-api.md](contracts/http-api.md) and [data-model.md](data-model.md).
