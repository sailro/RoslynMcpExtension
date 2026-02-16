using System.Collections.Generic;
using System.Threading.Tasks;
using RoslynMcpExtension.Shared.Models;

namespace RoslynMcpExtension.Shared;

/// <summary>
/// RPC interface for Roslyn code analysis operations.
/// Implemented by VS extension (in-process with VisualStudioWorkspace),
/// called by MCP server process via Named Pipes.
/// </summary>
public interface IRoslynAnalysisRpc
{
    Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers);

    Task<FindReferencesResult> FindReferencesAsync(string filePath, int line, int column, int maxResults);

    Task<GoToDefinitionResult> GoToDefinitionAsync(string filePath, int line, int column);

    Task<List<DocumentSymbolInfo>> GetDocumentSymbolsAsync(string filePath);

    Task<SearchSymbolsResult> SearchSymbolsAsync(string query, int maxResults);

    Task<SymbolDetailInfo> GetSymbolInfoAsync(string filePath, int line, int column);

    Task<List<ComplexityInfo>> AnalyzeComplexityAsync(string filePath);
}

/// <summary>
/// RPC interface for server-side operations.
/// Implemented by MCP server process, called by VS extension.
/// </summary>
public interface IServerRpc
{
    Task ShutdownAsync();
}
