# Quickstart: Validating the Model-Backed Generator

Prerequisites: .NET 8 SDK or later. No cloud account is needed for steps 1 to 3.

## 1. Automated suite (no model, no network, no configuration)

```powershell
dotnet test
```

Expected: all tests pass; the live check is reported as skipped and its skip reason names
`AGENTICCHANGEOPS_LIVE_MODEL_CHECK`. Slices 001 to 003 tests are unchanged and green.

## 2. Configuration errors are clear

The configuration tests cover each case with a dictionary standing in for the environment:
missing endpoint, missing deployment, placeholder values, non-positive limits, valid with
defaults. Expected: each failure names the variable and never shows the value.

## 3. Failure paths store nothing

The failure tests run the real drafting service over a fake model client that is unreachable,
slow, oversized, unparseable, blocked, or incomplete. Expected: a generation-failure error
naming the category, no draft, no audit entry, no version consumed, change request unchanged.

## 4. Live check (manual, explicit opt-in)

Requires access to an Azure OpenAI deployment through your own signed-in identity. Set these in
your shell session only; never commit them:

| Variable | Value |
|----------|-------|
| `AGENTICCHANGEOPS_MODEL_ENDPOINT` | `<your-resource-endpoint>` |
| `AGENTICCHANGEOPS_MODEL_DEPLOYMENT` | `<your-deployment-name>` |
| `AGENTICCHANGEOPS_LIVE_MODEL_CHECK` | `1` |

```powershell
dotnet test --filter "FullyQualifiedName~LiveModelCheck"
```

Expected: the test generates a draft for a synthetic change request and passes if the answer
parsed into the five sections. It prints no prompt, answer, endpoint, or deployment name.
Without `AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1` it is skipped.

Placeholder rule: a value that is blank, wrapped in `<...>` or `{...}`, starts with `YOUR_` or
`REPLACE`, or contains `placeholder` is rejected at creation.

## 5. Secret scan

```powershell
dotnet test --filter "FullyQualifiedName~NoSecrets"
```

Expected: no key, token, real endpoint, or deployment name found in source, tests, or specs.

See [contracts/AzureOpenAIDraftGenerator.md](contracts/AzureOpenAIDraftGenerator.md) and
[data-model.md](data-model.md) for the types and response format.
