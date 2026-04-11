using System.Collections.Generic;
using System.Threading.Tasks;

namespace RoslynMcpExtension.Shared;

public partial interface IRoslynAnalysisRpc
{
    Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers);
    Task<SymbolListResult> FindReferencesAsync(string filePath, int line, int column, int maxResults);
    Task<SymbolListResult> GoToDefinitionAsync(string filePath, int line, int column);
    Task<SymbolListResult> GetDocumentSymbolsAsync(string filePath);
    Task<SymbolListResult> SearchSymbolsAsync(string query, int maxResults);
    Task<SymbolListResult> FindDeadCodeAsync(int maxResults, bool includeInternal, bool includePublic);
    Task<SymbolInfoResult> GetSymbolInfoAsync(string filePath, int line, int column);
}

public interface IServerRpc
{
    Task ShutdownAsync();
}
