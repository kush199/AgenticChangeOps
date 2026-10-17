# Draft: Constitution v1.0.5 (PATCH)

**Change**: In "Technology Stack", the Agent Framework bullet currently says the framework runs the Architect's single request "as a stateless, tool-less agent" and that tools need their own slice. Replace the tool-less wording with:

> Used so far to run the Architect agent's content-generation request as a stateless agent behind the `IChatModelClient` and `IToolChatModelClient` seams. The Architect may be given read-only tools over a small synthetic sample codebase (`sample-codebase/`), offered through the Model Context Protocol by an in-process server in `AgenticChangeOps.SampleCode`; the tool set is fixed in code, reading is non-consequential, and tool output is untrusted data. It generates content only: routing, eligibility and every decision remain deterministic C# (Principle II), and framework and MCP types stay inside `AgenticChangeOps.ModelAccess` and `AgenticChangeOps.SampleCode`. Semantic Kernel is not used. Tools for any other agent, or memory, sessions or workflows, need their own slice and spec.

Update the "Tool/Resource Protocol" bullet to: "Model Context Protocol (MCP), used in-process for the Architect's sample codebase tools."

**Rationale**: slice 015 is the slice the 1.0.4 note called for; the note must match the code.

**Impact**: slice 011's behaviour is unchanged; no other slice is affected. Version line: `1.0.5 | Ratified 2026-10-10 | Last Amended <merge date>`.
