using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class GoToDefinitionTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_go_to_definition")]
	[Description("Navigates to the definition of a symbol at a given position. Returns source locations or metadata assembly names.")]
	public Task<SymbolListResult> GoToDefinition(
		[Description("Absolute path to the C# file")] string filePath,
		[Description("Line number (1-based)")] int line,
		[Description("Column number (1-based)")] int column)
		=> rpc.GoToDefinitionAsync(filePath, line, column);
}
