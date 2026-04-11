using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class GoToDefinitionService(DocumentFinder documentFinder)
{
	public async Task<SymbolListResult> GoToDefinitionAsync(string filePath, int line, int column)
	{
		var result = new SymbolListResult();

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

			result.Symbol = CodeMemberInfoFactory.Create(
				symbol,
				symbol.Name,
				"member",
				symbol.Locations.FirstOrDefault(l => l.IsInSource));

			foreach (var loc in symbol.Locations)
			{
				if (loc.IsInSource)
				{
					var lineSpan = loc.GetLineSpan();
					var sourceText = await loc.SourceTree!.GetTextAsync();
					var defLine = sourceText.Lines[lineSpan.StartLinePosition.Line];

					result.Members.Add(new SymbolLocation
					{
						Name = symbol.Name,
						FullName = defLine.ToString().Trim(),
						MemberType = "definition",
						FilePath = loc.SourceTree.FilePath,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1
					});
				}
				else if (loc.IsInMetadata)
				{
					result.Members.Add(new SymbolLocation
					{
						Name = symbol.Name,
						FullName = symbol.ToDisplayString(),
						MemberType = "metadata",
						FilePath = symbol.ContainingAssembly?.Name ?? "metadata"
					});
				}
			}
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}
}
