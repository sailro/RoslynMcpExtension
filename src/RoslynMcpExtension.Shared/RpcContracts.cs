using System.Collections.Generic;
using System.Threading.Tasks;
using PolyType;
using StreamJsonRpc;

namespace RoslynMcpExtension.Shared;

[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IRoslynAnalysisRpc
{
    Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers);
    Task<FindReferencesResult> FindReferencesAsync(string filePath, int line, int column, int maxResults);
    Task<GoToDefinitionResult> GoToDefinitionAsync(string filePath, int line, int column);
    Task<List<DocumentSymbolInfo>> GetDocumentSymbolsAsync(string filePath);
    Task<SearchSymbolsResult> SearchSymbolsAsync(string query, int maxResults);
    Task<SymbolDetailInfo> GetSymbolInfoAsync(string filePath, int line, int column);
}

public interface IServerRpc
{
    Task ShutdownAsync();
}
