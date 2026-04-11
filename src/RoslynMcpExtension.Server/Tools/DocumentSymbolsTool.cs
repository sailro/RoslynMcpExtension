using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class DocumentSymbolsTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_get_document_symbols")]
	[Description("Lists all symbols in a C# file as a tree (namespaces, types, members) with accessibility, modifiers, and line spans.")]
	public Task<SymbolListResult> GetDocumentSymbols(
		[Description("Absolute path to the C# file")] string filePath)
		=> rpc.GetDocumentSymbolsAsync(filePath);
}
