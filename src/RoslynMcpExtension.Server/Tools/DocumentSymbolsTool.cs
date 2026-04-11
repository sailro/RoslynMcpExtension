using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class DocumentSymbolsTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_get_document_symbols")]
	[Description("Lists all symbols (classes, methods, properties, fields, etc.) in a C# file with their types, accessibility modifiers, and line spans. Uses the live Roslyn semantic model.")]
	public async Task<DocumentSymbolsResult> GetDocumentSymbols(
		[Description("Absolute path to the C# file")] string filePath)
	{
		var symbols = await rpc.GetDocumentSymbolsAsync(filePath);
		return new DocumentSymbolsResult { Symbols = symbols };
	}
}
