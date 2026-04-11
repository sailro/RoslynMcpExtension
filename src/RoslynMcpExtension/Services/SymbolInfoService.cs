using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class SymbolInfoService(DocumentFinder documentFinder)
{
	public async Task<SymbolInfoResult> GetSymbolInfoAsync(string filePath, int line, int column)
	{
		try
		{
			var document = documentFinder.FindDocument(filePath);
			if (document == null)
				return new SymbolInfoResult { ErrorMessage = $"File not found in any project: {filePath}" };

			var semanticModel = await document.GetSemanticModelAsync();
			var syntaxTree = await document.GetSyntaxTreeAsync();
			if (semanticModel == null || syntaxTree == null)
				return new SymbolInfoResult { ErrorMessage = "Failed to get semantic model" };

			var position = DocumentFinder.GetPosition(syntaxTree, line, column);
			var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, documentFinder.Workspace);
			if (symbol == null)
				return new SymbolInfoResult { ErrorMessage = $"No symbol found at line {line}, column {column}" };

			return CodeMemberInfoFactory.CreateSymbolInfo(symbol, symbol.Locations.FirstOrDefault(l => l.IsInSource));
		}
		catch (Exception ex)
		{
			return new SymbolInfoResult { ErrorMessage = ex.Message };
		}
	}
}
