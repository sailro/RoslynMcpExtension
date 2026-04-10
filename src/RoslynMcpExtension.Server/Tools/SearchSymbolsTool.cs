using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class SearchSymbolsTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_search_symbols")]
    [Description("Searches for symbol declarations across the entire solution by name. Uses Roslyn's SymbolFinder for accurate results matching types, methods, properties, and fields.")]
    public async Task<string> SearchSymbols(
        [Description("Search query (symbol name or partial name, case-insensitive)")] string query,
        [Description("Maximum number of results to return (default: 30)")] int maxResults = 30)
    {
        var result = await rpc.SearchSymbolsAsync(query, maxResults);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        if (result.Members.Count == 0)
            return $"No symbols found matching '{query}'.";

        var sb = new StringBuilder();
        sb.AppendLine($"Search results for '{query}' ({result.TotalCount}{(result.Truncated ? " truncated" : "")}):");

        foreach (var s in result.Members)
        {
            sb.AppendLine($"  {s.MemberType} {s.FullName}");
            sb.AppendLine($"    {s.FilePath}:{s.StartLine}:{s.StartColumn}");
        }

        return sb.ToString();
    }
}
