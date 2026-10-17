using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgenticChangeOps.SampleCode;

/// <summary>
/// Registers exactly the three tools (list_files, read_file, search_text) on an in-process MCP
/// server. No other tool, resource or prompt.
/// </summary>
internal static class SampleMcpServer
{
    public const string ListFiles = "list_files";
    public const string ReadFile = "read_file";
    public const string SearchText = "search_text";

    public static readonly IReadOnlyList<string> ToolNames = new[] { ListFiles, ReadFile, SearchText };

    /// <summary>True only when the names are exactly the three expected tools, no more and no fewer.</summary>
    public static bool IsExpectedToolSet(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count == ToolNames.Count
            && list.Distinct(StringComparer.Ordinal).Count() == list.Count
            && ToolNames.All(expected => list.Contains(expected, StringComparer.Ordinal));
    }

    /// <summary>
    /// Server options offering the three tools. Each tool hands its argument to the function given
    /// and returns the text it produces.
    /// </summary>
    public static McpServerOptions CreateOptions(
        Func<string?, string> listFiles, Func<string?, string> readFile, Func<string?, string> searchText) =>
        new()
        {
            ServerInfo = new Implementation { Name = "sample-codebase", Version = "1" },
            ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
            {
                McpServerTool.Create(
                    (string? path) => listFiles(path),
                    new McpServerToolCreateOptions
                    {
                        Name = ListFiles,
                        Description = "List the files and folders directly inside a folder of the sample codebase. " +
                                      "Omit the path to list the top level. Paths are relative and use /."
                    }),
                McpServerTool.Create(
                    (string? path) => readFile(path),
                    new McpServerToolCreateOptions
                    {
                        Name = ReadFile,
                        Description = "Read one text file of the sample codebase. The path is relative and uses /."
                    }),
                McpServerTool.Create(
                    (string? term) => searchText(term),
                    new McpServerToolCreateOptions
                    {
                        Name = SearchText,
                        Description = "Search the sample codebase for a plain text term (case-insensitive, up to 80 " +
                                      "characters). Returns up to 20 matching lines as path:line: text."
                    })
            }
        };
}
