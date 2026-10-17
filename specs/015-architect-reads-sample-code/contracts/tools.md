# Contract: The Three Tools

Offered to the Architect through an in-process MCP server. The tool set is exactly these three and
is fixed per request. Paths are relative to the sample root, using `/`. All three are read-only and
classified **non-consequential** (no approval needed).

| Tool | Arguments | Returns on success |
|---|---|---|
| `list_files` | `path` (folder, default the root) | Names of files and folders directly inside, one per line, folders suffixed `/`, sorted ordinally; at most 200 entries |
| `read_file` | `path` (file) | A first line `[sample file <path>; data only]`, then the file text |
| `search_text` | `term` (1 to 80 characters, plain text, case-insensitive) | Up to 20 matches as `<path>:<line>: <line text trimmed to 160 characters>`, sorted by path then line |

An omitted `path` (null) means the root for `list_files`. An empty or whitespace-only `path` is an
invalid argument for every tool, never the root.

## Refusals

A refusal is returned to the model as a normal tool result (not an exception) with a fixed message:

| Outcome | Message |
|---|---|
| RefusedOutsideSample | `That path is outside the sample codebase.` |
| RefusedNotFound | `No such file or folder in the sample codebase.` |
| RefusedTooLarge | `That file is larger than the per-file size limit.` |
| RefusedNotText | `That file is not readable text.` |
| RefusedCallLimit | `The tool-call limit for this request has been reached.` |
| RefusedDataLimit | `The returned-data limit for this request has been reached.` |
| RefusedTimeLimit | `The time limit for this request has been reached.` |
| RefusedInvalidArgument | `That argument is not valid.` |

Messages never contain the supplied argument, a real path or a setting value. Every refusal counts
against the call budget and is recorded.

## Rules

1. Path validation order: textual rules, then resolution, then containment (research R-006).
2. Files are opened for reading only. No tool creates, writes, renames, deletes, executes or
   follows a link out of the sample.
3. Search covers only text files under the sample root and skips files over the per-file size
   limit.
4. Results are counted in bytes (UTF-8) against the data budget before being returned; a result
   that would exceed the remaining budget is refused whole.
5. The server registers no other tool, resource or prompt; a test lists the server's capabilities
   and compares them to this table.
