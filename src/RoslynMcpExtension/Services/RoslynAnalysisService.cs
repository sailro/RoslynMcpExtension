using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServices;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

[Export(typeof(RoslynAnalysisService))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RoslynAnalysisService(VisualStudioWorkspace workspace) : IRoslynAnalysisRpc
{
	private readonly DocumentFinder _documentFinder = new(workspace);

	internal OutputLogger? Logger { get; set; }

	public Task LogAsync(string message)
	{
		Logger?.Log($"[MCP Server] {message}");
		return Task.CompletedTask;
	}

	public Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers)
		=> InvokeAsync(nameof(ValidateFileAsync),
			() => new ValidateFileService(_documentFinder).ValidateFileAsync(filePath, includeWarnings, runAnalyzers));

	public Task<SymbolListResult> FindReferencesAsync(string filePath, int line, int column, int maxResults)
		=> InvokeAsync(nameof(FindReferencesAsync),
			() => new FindReferencesService(_documentFinder).FindReferencesAsync(filePath, line, column, maxResults));

	public Task<SymbolListResult> GoToDefinitionAsync(string filePath, int line, int column)
		=> InvokeAsync(nameof(GoToDefinitionAsync),
			() => new GoToDefinitionService(_documentFinder).GoToDefinitionAsync(filePath, line, column));

	public Task<SymbolListResult> GetDocumentSymbolsAsync(string filePath)
		=> InvokeAsync(nameof(GetDocumentSymbolsAsync),
			() => new DocumentSymbolsService(_documentFinder).GetDocumentSymbolsAsync(filePath));

	public Task<SymbolListResult> SearchSymbolsAsync(string query, int maxResults)
		=> InvokeAsync(nameof(SearchSymbolsAsync),
			() => new SearchSymbolsService(_documentFinder).SearchSymbolsAsync(query, maxResults));

	public Task<SymbolListResult> FindDeadCodeAsync(int maxResults, bool includeInternal, bool includePublic)
		=> InvokeAsync(nameof(FindDeadCodeAsync),
			() => new DeadCodeAnalysisService(_documentFinder).FindDeadCodeAsync(maxResults, includeInternal, includePublic));

	public Task<SymbolInfoResult> GetSymbolInfoAsync(string filePath, int line, int column)
		=> InvokeAsync(nameof(GetSymbolInfoAsync),
			() => new SymbolInfoService(_documentFinder).GetSymbolInfoAsync(filePath, line, column));

	private async Task<T> InvokeAsync<T>(string toolName, Func<Task<T>> action)
	{
		Logger?.Log($"Tool '{toolName}' invoked");
		var sw = Stopwatch.StartNew();
		try
		{
			var result = await action();
			Logger?.Log($"Tool '{toolName}' completed in {sw.ElapsedMilliseconds}ms");
			return result;
		}
		catch (Exception ex)
		{
			Logger?.Log($"Tool '{toolName}' failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
			throw;
		}
	}
}
