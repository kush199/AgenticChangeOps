# Contract: Shared Pieces

The three shared pieces are the only new public surface. Signatures and guarantees are in [data-model.md](../data-model.md); this contract lists what a caller (an agent) may rely on and what it must supply. Each guarantee has one test in `ModelAccess.Tests`.

## ModelCaller

| Guarantee | Test class |
|---|---|
| Exactly one client call per request; never retried | `ModelCallerTests` |
| Output bound is `ceil(MaxResponseCharacters / 3)` | `ModelCallerTests` |
| Client's own categorised failure passes through unchanged | `ModelCallerTests` |
| Caller cancellation propagates as cancellation, not a category | `ModelCallerTests` |
| Time-limit expiry becomes `TimedOut` | `ModelCallerTests` |
| Any other exception becomes the mapped category; original not attached; message and type name absent from the carried exception | `ModelCallerTests` |
| Cut-off or over-length answer becomes `ResponseTooLarge`; empty or blank becomes `MalformedOutput` | `ModelCallerTests` |
| Null options or client is rejected at construction | `ModelCallerTests` |

Caller supplies: the instruction and user text (the prompt is the agent's), the options, the client.

## ModelAnswerReader / ModelAnswer

| Guarantee | Test class |
|---|---|
| Bare object, unlabelled fence, `json` fence (any case) accepted | `ModelAnswerReaderTests` |
| Other label, no newline after opening fence, missing closing fence, extra fence inside, non-object root, invalid JSON: malformed | `ModelAnswerReaderTests` |
| Missing key, non-string text, non-array list, non-string list item: malformed | `ModelAnswerReaderTests` |
| Present-but-empty text and empty list are returned unchanged | `ModelAnswerReaderTests` |
| An exception thrown by the caller's map becomes malformed; no answer text in the exception | `ModelAnswerReaderTests` |

Caller supplies: the section names and the content type to build.

## SectionNormaliser

| Guarantee | Test class |
|---|---|
| Text: trimmed; blank is an error; limit is 2000 characters inclusive | `SectionNormaliserTests` |
| List: missing is an error; blanks dropped; items trimmed; order kept | `SectionNormaliserTests` |
| List: `requireItem` true makes an empty result an error, false accepts it | `SectionNormaliserTests` |
| List: 50 items accepted, 51 is an error; item of 2000 characters accepted, 2001 is an error | `SectionNormaliserTests` |
| Error wording per case is exactly as listed in the data model | `SectionNormaliserTests` |

Caller supplies: the section name (used in the wording) and whether an item is required.

## Third-agent proof

`StandInAgentTests` builds a throwaway agent (own section names, one required list, one optional list) using all three pieces, with its own content type, and shows it needs no change to ModelAccess or to either agent project.
