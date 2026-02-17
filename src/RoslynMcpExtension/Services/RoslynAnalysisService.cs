using System.Collections.Generic;
using System.ComponentModel.Composition;
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

	public Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers)
		=> new ValidateFileService(_documentFinder).ValidateFileAsync(filePath, includeWarnings, runAnalyzers);

	public Task<FindReferencesResult> FindReferencesAsync(string filePath, int line, int column, int maxResults)
		=> new FindReferencesService(_documentFinder).FindReferencesAsync(filePath, line, column, maxResults);

	public Task<GoToDefinitionResult> GoToDefinitionAsync(string filePath, int line, int column)
		=> new GoToDefinitionService(_documentFinder).GoToDefinitionAsync(filePath, line, column);

	public Task<List<DocumentSymbolInfo>> GetDocumentSymbolsAsync(string filePath)
		=> new DocumentSymbolsService(_documentFinder).GetDocumentSymbolsAsync(filePath);

	public Task<SearchSymbolsResult> SearchSymbolsAsync(string query, int maxResults)
		=> new SearchSymbolsService(_documentFinder).SearchSymbolsAsync(query, maxResults);

	public Task<SymbolDetailInfo> GetSymbolInfoAsync(string filePath, int line, int column)
		=> new SymbolInfoService(_documentFinder).GetSymbolInfoAsync(filePath, line, column);
}
