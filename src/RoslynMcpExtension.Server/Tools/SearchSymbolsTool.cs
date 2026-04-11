using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class SearchSymbolsTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_search_symbols")]
	[Description("Searches for symbol declarations across the entire solution by name or partial name.")]
	public Task<SymbolListResult> SearchSymbols(
		[Description("Search query (symbol name or partial name, case-insensitive)")] string query,
		[Description("Maximum number of results to return (default: 30)")] int maxResults = 30)
		=> rpc.SearchSymbolsAsync(query, maxResults);
}
