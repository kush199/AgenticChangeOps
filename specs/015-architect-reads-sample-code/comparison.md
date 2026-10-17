# Comparison: tools off against the recordings taken before slice 015

| Surface | Before | After (defaults) | Result |
|---|---|---|---|
| Design route responses, latest, history, audit listing (golden design-operations transcript) | recorded | same file, unmodified, passing | identical |
| Stored design payload (no file list) | baseline/stored-design-payload.json | same bytes | identical |
| Stored audit payload (DesignGenerated) | baseline/stored-audit-entry.json | same bytes | identical |
| Tools-off system instruction | recorded in ToolsOffGeneratorTests | same text | identical |
| Model calls per design | one, through IChatModelClient | one, through IChatModelClient | identical |
| Existing tests | 1073 (2 live skipped) | all pass; 3 approved edits (see off-run.txt) | as agreed |

## Additive members actually added (all null by default and omitted from JSON when null)

- `DesignDraft.SampleFilesRead` (last parameter)
- `DesignContent.SampleFilesRead`, `DesignContent.ToolCalls` (trailing optional)
- `ChangeRequestAuditEntry.ToolName`, `ToolTarget`, `ToolOutcome`, `ResultBytes`, `ElapsedMilliseconds`
- `AuditEventType.ToolCalled` (appended)
- `GenerationFailureCategory.ToolsUnavailable`, `LimitsExhausted` (appended)

Storage layout version stays 2.
