# Draft: constitution amendment 1.0.3 -> 1.0.4 (PATCH)

Raise as its own `docs:` PR on branch `docs/constitution-1-0-4`, merged before the slice PR (as for 1.0.2 and 1.0.3).

## Change to "Technology Stack"

Replace

    - **Agent Framework**: Microsoft Agent Framework (Semantic Kernel Agent Framework)

with

    - **Agent Framework**: Microsoft Agent Framework. Used so far to run the Architect agent's single
      content-generation request as a stateless, tool-less agent behind the `IChatModelClient` seam, so the
      framework can be studied and compared on one agent. It generates content only: routing, eligibility and
      every decision remain deterministic C# (Principle II), and framework types stay inside
      `AgenticChangeOps.ModelAccess`. Semantic Kernel is not used. Using it for another agent, or for tools,
      memory, sessions or workflows, needs its own slice and spec.

Footer: `**Version**: 1.0.4 | **Ratified**: 2026-10-10 | **Last Amended**: <merge date>`

## Rationale for the PR body

The stack note already names the framework but described it as the Semantic Kernel agent framework, which
is not what is used. This records how and why it is actually used. No principle is added or redefined.
Impact: none on existing slices; documentation of the slice 011 choice. Compliance: Principle II unaffected.

Also record in the PR: the shared model-access project now runs on `Azure.AI.OpenAI` 2.9.0-beta.1 (prerelease)
because the framework packages require `OpenAI` >= 2.14, which 2.1.0 of `Azure.AI.OpenAI` cannot run on.
