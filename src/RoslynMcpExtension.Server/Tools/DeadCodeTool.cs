using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class DeadCodeTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_find_dead_code")]
	[Description("Finds potentially dead code (types, methods, fields with no source references) in the active workspace.")]
	public Task<SymbolListResult> FindDeadCode(
		[Description("Maximum number of dead code entries to return (default: 200)")] int maxResults = 200,
		[Description("Include internal and private members (default: true). When false, only private members are reported unless includePublic is also true.")] bool includeInternal = true,
		[Description("Include public, protected, and protected internal members (default: false). This is broader and may include externally used APIs.")] bool includePublic = false)
		=> rpc.FindDeadCodeAsync(maxResults, includeInternal, includePublic);
}
