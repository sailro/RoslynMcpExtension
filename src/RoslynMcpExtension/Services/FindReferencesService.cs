using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class FindReferencesService(DocumentFinder documentFinder)
{
	public async Task<SymbolListResult> FindReferencesAsync(string filePath, int line, int column, int maxResults)
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

			var references = await SymbolFinder.FindReferencesAsync(symbol, documentFinder.Workspace.CurrentSolution);
			var count = 0;

			foreach (var refSymbol in references)
			{
				foreach (var loc in refSymbol.Definition.Locations.Where(l => l.IsInSource))
				{
					if (count >= maxResults) break;
					var lineSpan = loc.GetLineSpan();
					var sourceText = await loc.SourceTree!.GetTextAsync();
					var refLine = sourceText.Lines[lineSpan.StartLinePosition.Line];

					result.Members.Add(new SymbolLocation
					{
						Name = refSymbol.Definition.Name,
						FullName = refSymbol.Definition.ToDisplayString(),
						MemberType = "definition",
						FilePath = loc.SourceTree.FilePath,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1
					});
					count++;
				}

				foreach (var loc in refSymbol.Locations)
				{
					if (count >= maxResults) break;
					var lineSpan = loc.Location.GetLineSpan();

					var containingMember = DocumentFinder.GetContainingMemberName(
						await loc.Location.SourceTree!.GetRootAsync(), loc.Location.SourceSpan);

					result.Members.Add(new SymbolLocation
					{
						Name = containingMember ?? refSymbol.Definition.Name,
						FullName = refSymbol.Definition.ToDisplayString(),
						MemberType = "reference",
						FilePath = loc.Location.SourceTree.FilePath,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1
					});
					count++;
				}
			}

			result.TotalCount = count;
			result.Truncated = count >= maxResults;
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}
}
