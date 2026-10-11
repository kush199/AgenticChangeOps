# Data Model: Model-Backed Requirements Draft Generator

No persisted record changes. `DraftContent`, `RequirementsDraft`, `DraftingResult`,
`ValidationError` and the audit types from slices 001 to 003 are reused unchanged.

## GenerationFailureCategory (new enum, core library)

| Value | Meaning |
|-------|---------|
| `Unreachable` | Model service not reachable, identity unavailable or rejected, throttled, or a service-side error |
| `TimedOut` | No complete answer within the configured time limit |
| `ResponseTooLarge` | Answer exceeded the size limit or was cut off by it |
| `MalformedOutput` | Answer blocked, empty, or not parseable into the required structure |

## DraftGenerationException (new, core library)

| Member | Type | Notes |
|--------|------|-------|
| `Category` | `GenerationFailureCategory` | The only detail carried |

The message is a fixed string per category. It never contains prompt text, model output,
endpoint, deployment name, or identity detail, and no inner exception is attached.

## AzureOpenAIGeneratorOptions (new, adapter library)

| Member | Type | Rule |
|--------|------|------|
| `Endpoint` | `Uri` | Required; absolute `https`; not a placeholder |
| `DeploymentName` | `string` | Required; non-blank; not a placeholder |
| `RequestTimeout` | `TimeSpan` | Positive; default 30 s |
| `MaxResponseCharacters` | `int` | Positive; default 32,000 |

Built by `FromEnvironment(Func<string, string?>)` so tests supply a dictionary instead of the
real environment. Validation failures throw `GeneratorConfigurationException`.

## GeneratorConfigurationException (new, adapter library)

| Member | Type | Notes |
|--------|------|-------|
| `SettingName` | `string` | The environment variable that is wrong |

The message names the setting and the problem (missing, blank, placeholder, not a positive
number, not an absolute https address). It never includes the supplied value.

## ChatModelResponse (new record, adapter library)

| Member | Type | Notes |
|--------|------|-------|
| `Text` | `string` | The model's answer text, possibly empty |
| `WasCutOff` | `bool` | True when the service stopped at the output limit |

## Response format (what the model is asked to return)

```json
{
  "summary": "string",
  "functionalRequirements": ["string"],
  "acceptanceCriteria": ["string"],
  "assumptions": ["string"],
  "openQuestions": ["string"]
}
```

Mapping to `DraftContent`: each key maps to the member of the same meaning. A missing key or a
wrong shape is `MalformedOutput`; a present but empty value is passed through for slice 003
validation.

## State transitions

None. The generator holds no state between calls and each call is independent.
