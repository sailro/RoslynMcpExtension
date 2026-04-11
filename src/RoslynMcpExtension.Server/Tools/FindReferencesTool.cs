using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class FindReferencesTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_find_references")]
	[Description("Finds all references to a symbol at a given position using Roslyn's semantic analysis. Returns definition and usage locations across the entire solution. Much more accurate than text search.")]
	public Task<FindReferencesResult> FindReferences(
		[Description("Absolute path to the C# file")] string filePath,
		[Description("Line number (1-based)")] int line,
		[Description("Column number (1-based)")] int column,
		[Description("Maximum number of results to return (default: 50)")] int maxResults = 50)
		=> rpc.FindReferencesAsync(filePath, line, column, maxResults);
}
