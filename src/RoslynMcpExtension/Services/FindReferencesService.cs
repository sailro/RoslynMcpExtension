using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class FindReferencesService(DocumentFinder documentFinder)
{
	public async Task<FindReferencesResult> FindReferencesAsync(string filePath, int line, int column, int maxResults)
	{
		var result = new FindReferencesResult();

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
			result.Member = CodeMemberInfoFactory.CreateDetailed(
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

					result.References.Add(new ReferenceLocationInfo
					{
						FilePath = loc.SourceTree.FilePath,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1,
						EndLine = lineSpan.EndLinePosition.Line + 1,
						EndColumn = lineSpan.EndLinePosition.Character + 1,
						Preview = refLine.ToString().Trim(),
						IsDefinition = true
					});
					count++;
				}

				foreach (var loc in refSymbol.Locations)
				{
					if (count >= maxResults) break;
					var lineSpan = loc.Location.GetLineSpan();
					var sourceText = await loc.Location.SourceTree!.GetTextAsync();
					var refLine = sourceText.Lines[lineSpan.StartLinePosition.Line];

					var containingMember = DocumentFinder.GetContainingMemberName(
						await loc.Location.SourceTree.GetRootAsync(), loc.Location.SourceSpan);

					result.References.Add(new ReferenceLocationInfo
					{
						FilePath = loc.Location.SourceTree.FilePath,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1,
						EndLine = lineSpan.EndLinePosition.Line + 1,
						EndColumn = lineSpan.EndLinePosition.Character + 1,
						Preview = refLine.ToString().Trim(),
						ContainingMember = containingMember,
						IsDefinition = false
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
