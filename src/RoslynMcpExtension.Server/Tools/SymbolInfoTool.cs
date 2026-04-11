using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class SymbolInfoTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_get_symbol_info")]
	[Description("Gets detailed type and metadata information for a symbol at a specific position. Returns accessibility, modifiers, base types, interfaces, parameters, and XML documentation.")]
	public Task<MemberLookupResult> GetSymbolInfo(
		[Description("Absolute path to the C# file")] string filePath,
		[Description("Line number (1-based)")] int line,
		[Description("Column number (1-based)")] int column)
		=> rpc.GetSymbolInfoAsync(filePath, line, column);
}
