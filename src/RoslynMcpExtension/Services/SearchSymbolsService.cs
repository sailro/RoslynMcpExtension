using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class SearchSymbolsService(DocumentFinder documentFinder)
{
	public async Task<SearchSymbolsResult> SearchSymbolsAsync(string query, int maxResults)
	{
		var result = new SearchSymbolsResult();

		try
		{
			var solution = documentFinder.Workspace.CurrentSolution;

			foreach (var project in solution.Projects)
			{
				if (result.Symbols.Count >= maxResults) break;

				var symbols = await SymbolFinder.FindDeclarationsAsync(
					project, query, ignoreCase: true,
					filter: SymbolFilter.TypeAndMember,
					cancellationToken: CancellationToken.None);

				foreach (var symbol in symbols)
				{
					if (result.Symbols.Count >= maxResults) break;
					if (symbol.Locations.Length == 0) continue;

					var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
					if (loc == null) continue;

					var lineSpan = loc.GetLineSpan();
					result.Symbols.Add(new SymbolSearchInfo
					{
						Name = symbol.Name,
						FullName = symbol.ToDisplayString(),
						Kind = symbol.Kind.ToString(),
						FilePath = loc.SourceTree?.FilePath ?? string.Empty,
						StartLine = lineSpan.StartLinePosition.Line + 1,
						StartColumn = lineSpan.StartLinePosition.Character + 1,
						ContainingType = symbol.ContainingType?.ToDisplayString(),
						ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString()
					});
				}
			}

			result.TotalCount = result.Symbols.Count;
			result.Truncated = result.Symbols.Count >= maxResults;
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}
}
