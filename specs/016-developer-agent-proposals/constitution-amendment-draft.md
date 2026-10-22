# Constitution Amendment Draft: v1.0.5 to v1.0.6 (PATCH)

**Opened as its own PR (T049): https://github.com/kush199/AgenticChangeOps/pull/25. Must merge before the slice 016 PR.**

## Change

In **Technology Stack**, the **Agent Framework** and **Tool/Resource Protocol** entries currently say
the Architect agent may be given read-only tools over the synthetic sample codebase. Amend them so
that:

- The **Developer agent** may use the same read-only sample codebase tools, offered through the Model
  Context Protocol by the same in-process server, under the same operator switch and limits, to
  produce a **text-only change proposal** (summary, proposed edits, risks, test ideas, open questions)
  that the system verifies against the real sample files before storing.
- Producing a proposal is non-consequential (nothing is written, created, deleted, compiled, run or
  applied). Applying a proposal would be consequential and needs its own slice, spec and human
  approval gate.
- The model still only generates content; routing, eligibility, verification and every decision stay
  deterministic C# (Principle II). Framework and MCP types stay inside `AgenticChangeOps.ModelAccess`
  and `AgenticChangeOps.SampleCode`.
- Using the framework or tools for any other agent, or for memory, sessions or workflows, still needs
  its own slice and spec.

Bump `Version` to 1.0.6 and `Last Amended` to the date of merge.

## Rationale

Slice 016 is the first use of the sample tools by an agent other than the Architect. The Technology
Stack says that needs its own slice and spec, and records the Architect as the only user. A PATCH
bump is enough: no principle is added, removed or redefined.

## Impact

- Slices 011 and 015: unchanged behaviour; the wording about "the Architect agent's" tools becomes
  "the Architect and Developer agents'".
- Slice 016: the only slice that depends on this wording.
- No in-progress work is invalidated.
