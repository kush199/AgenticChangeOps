# Contract: Where framework types may appear

| # | Rule | Check |
|---|---|---|
| D1 | `Microsoft.Agents.*` and `Microsoft.Extensions.AI*` namespaces are used only in `src/AgenticChangeOps.ModelAccess` | Structure test scanning `src` for those namespaces |
| D2 | Only `AgentFrameworkChatModelClient` names framework types inside ModelAccess | Same test, file-level |
| D3 | Package references to the framework appear only in the ModelAccess project file | Structure test over `*.csproj` |
| D4 | Architect project still references only Intake and ModelAccess | Existing Architect StructureTests, unchanged |
| D5 | Requirement project source is byte-for-byte unchanged | `git diff` of `src/AgenticChangeOps.Agents.Requirement` is empty |
| D6 | Intake, Workflow sources unchanged | `git diff` empty |
| D7 | `AzureOpenAIChatModelClient` behaviour unchanged (only the probe call is delegated) | Existing tests plus hand-written-client wire baseline |
