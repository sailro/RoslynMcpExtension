using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class SymbolInfoService(DocumentFinder documentFinder)
{
	public async Task<MemberLookupResult> GetSymbolInfoAsync(string filePath, int line, int column)
	{
		var result = new MemberLookupResult();

		try
		{
			var document = documentFinder.FindDocument(filePath);
			if (document == null)
			{
				result.ErrorMessage = $"File not found in any project: {filePath}";
				return result;
			}

			var semanticModel = await document.GetSemanticModelAsync();
			var syntaxTree = await document.GetSyntaxTreeAsync();
			if (semanticModel == null || syntaxTree == null)
			{
				result.ErrorMessage = "Failed to get semantic model";
				return result;
			}

			var position = DocumentFinder.GetPosition(syntaxTree, line, column);
			var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, documentFinder.Workspace);
			if (symbol == null)
			{
				result.ErrorMessage = $"No symbol found at line {line}, column {column}";
				return result;
			}

			result.Found = true;
			var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
			result.Member = CodeMemberInfoFactory.CreateDetailed(symbol, symbol.Name, "member", location);
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}
}
