# Contract: AzureOpenAIDraftGenerator and its seam

Namespace `AgenticChangeOps.Intake.AzureOpenAI` unless stated. In-process .NET types; no HTTP,
MCP, or CLI surface.

## IRequirementsDraftGenerator (existing, slice 003, unchanged)

```csharp
Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken);
```

`AzureOpenAIDraftGenerator` implements it.

| Outcome | Behaviour |
|---------|-----------|
| Well-formed answer | Returns `DraftContent` with the five sections |
| Any model failure | Throws `DraftGenerationException` with exactly one `Category` |
| Caller cancels | Propagates `OperationCanceledException`, not a category |

Guarantees: one model call per invocation; no retry; no writes; the `ChangeRequest` is not
mutated; nothing from the prompt or answer appears in any exception message.

## Creation

```csharp
public sealed class AzureOpenAIDraftGenerator : IRequirementsDraftGenerator
{
    public AzureOpenAIDraftGenerator(AzureOpenAIGeneratorOptions options, IChatModelClient client);

    // Reads options from the environment and builds the identity-authenticated client.
    public static AzureOpenAIDraftGenerator CreateFromEnvironment();
}
```

`CreateFromEnvironment` throws `GeneratorConfigurationException` before any draft request can
be made. Settings are validated once; they are not re-read per request.

## Configuration

See research decision 4 for the variable list and defaults, and decision 5 for the placeholder
rule. Errors name the variable, never its value.

## IChatModelClient (the seam)

```csharp
public interface IChatModelClient
{
    Task<ChatModelResponse> CompleteAsync(
        string systemInstruction,
        string userContent,
        int maxOutputTokens,
        CancellationToken cancellationToken);
}
```

Implementations either return a `ChatModelResponse` or throw `DraftGenerationException` with a
category. The real adapter maps service and identity errors using `ModelFailureMapper`; fakes
return or throw directly. The adapter never retries and never logs.

## Prompt contract

| Part | Content |
|------|---------|
| System instruction | Role; the required JSON keys and shapes; "use only the data provided"; "return only the JSON object" |
| User content | A fixed delimiter line, then one JSON object holding title, description, affected system, priority, change type |
| Not sent | Requester, change request id, status, timestamps, other requests, drafts, audit history, configuration |

## Response contract

See data-model.md, "Response format". Accepted: the object alone, or the object inside one
code fence. Everything else is `MalformedOutput`.

## Slice 003 service change

`RequirementsDraftingService.DraftAsync` builds its generator-failure error as before, but when
the caught exception is a `DraftGenerationException` the message adds the category:

| Caught | `ValidationError` |
|--------|-------------------|
| `DraftGenerationException` | `("Generator", "The content generator failed (<Category>).")` |
| any other exception | unchanged: `("Generator", "The content generator failed (<ExceptionTypeName>).")` |

No other service behaviour changes.
