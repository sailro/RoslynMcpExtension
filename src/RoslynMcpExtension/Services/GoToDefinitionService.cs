using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class GoToDefinitionService(DocumentFinder documentFinder)
{
	public async Task<GoToDefinitionResult> GoToDefinitionAsync(string filePath, int line, int column)
	{
		var result = new GoToDefinitionResult();

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
			result.Member = CodeMemberInfoFactory.Create(
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

					result.Definitions.Add(new DefinitionLocationInfo
					{
						FilePath = loc.SourceTree.FilePath,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1,
						EndLine = lineSpan.EndLinePosition.Line + 1,
						EndColumn = lineSpan.EndLinePosition.Character + 1,
						Preview = defLine.ToString().Trim(),
						IsFromMetadata = false
					});
				}
				else if (loc.IsInMetadata)
				{
					result.Definitions.Add(new DefinitionLocationInfo
					{
						FilePath = symbol.ContainingAssembly?.Name ?? "metadata",
						Preview = symbol.ToDisplayString(),
						IsFromMetadata = true,
						AssemblyName = symbol.ContainingAssembly?.Name
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
