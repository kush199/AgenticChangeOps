# Contract: HTTP Operations For Proposals

Same conventions as the design operations: camel-case JSON, string enums, the shared error body
(`code`, `message`, optional `fields`, optional `category`), id parsed as a GUID.

| Operation | Route | Success |
|---|---|---|
| Request a proposal | `POST /change-requests/{id}/proposals` (no body) | 201, `Location: /change-requests/{id}/proposals/latest`, body `ChangeProposal` |
| Latest proposal | `GET /change-requests/{id}/proposals/latest` | 200 `ChangeProposal` |
| Proposal history | `GET /change-requests/{id}/proposals` | 200 list in ascending version order |

## Errors

| Status | Code | When | Notes |
|---|---|---|---|
| 400 | `invalid-identifier` | id is not a valid GUID | all three routes |
| 404 | `not-found` | no such change request | all three routes; checked before availability |
| 404 | `no-proposal` | latest requested and none exists | latest route only |
| 503 | `proposal-not-configured` | model settings or proposal limits invalid | before any model call |
| 503 | `proposal-tools-unavailable` | sample tools switched off | before any model call |
| 409 | `conflict` | not Approved (`Status`), no requirements draft (`RequirementsDraft`), no design draft (`DesignDraft`), design not based on latest requirements (`DesignDraftVersion`), or any of these found true again at store time | `fields` name the failing rule; messages are fixed text |
| 502 | `generation-failed` | model failed, timed out, answer too large, answer malformed, tools could not be opened (`ToolsUnavailable`), limits exhausted (`LimitsExhausted`) | `category` only |
| 502 | `proposal-rejected` | the system refused the model's proposal | `category` is a `ProposalRejection` name; no path, quote or model text |
| 500 | `unexpected-error` | anything unforeseen | fixed text |
| 503 | `storage-unavailable`, 409 `version-conflict` | as for designs | unchanged shapes |

Never present in any error, log or response: file content, quoted or replacement text in an error,
model text, endpoint, deployment name, credential, or a path outside the sample.

## Response body (`ChangeProposal`)

```json
{
  "proposalId": "…", "changeRequestId": "…", "version": 1,
  "requirementsVersion": 1, "designVersion": 1,
  "generatedAt": "…", "status": "Draft", "agentName": "DeveloperAgent",
  "summary": "…",
  "changes": [
    { "path": "orders-service/config/orders.settings", "kind": "Change",
      "existingText": "…", "replacementText": "…" }
  ],
  "risks": ["…"], "testIdeas": ["…"], "openQuestions": [],
  "sampleFilesRead": ["orders-service/config/orders.settings"],
  "verificationNote": "Not compiled, run or tested."
}
```

## Unchanged

No existing route, status code, header or body changes. The only existing-shape addition is the
optional `offeredStep` on the workflow status body, present only when the Developer is available and the
stage is `DesignComplete`.
