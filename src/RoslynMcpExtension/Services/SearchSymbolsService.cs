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
	public async Task<SymbolListResult> SearchSymbolsAsync(string query, int maxResults)
	{
		var result = new SymbolListResult();

		try
		{
			var solution = documentFinder.Workspace.CurrentSolution;

			foreach (var project in solution.Projects)
			{
				if (result.Members.Count >= maxResults) break;

				var symbols = await SymbolFinder.FindDeclarationsAsync(
					project, query, ignoreCase: true,
					filter: SymbolFilter.TypeAndMember,
					cancellationToken: CancellationToken.None);

				foreach (var symbol in symbols)
				{
					if (result.Members.Count >= maxResults) break;
					if (symbol.Locations.Length == 0) continue;

					var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
					if (loc == null) continue;

					result.Members.Add(CodeMemberInfoFactory.Create(
						symbol,
						symbol.Name,
						"member",
						loc,
						project.Name));
				}
			}

			result.TotalCount = result.Members.Count;
			result.Truncated = result.Members.Count >= maxResults;
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}
}
