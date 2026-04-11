using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using RoslynMcpExtension.Shared;
using StreamJsonRpc;

namespace RoslynMcpExtension.Server;

/// <summary>
/// Named pipe JSON-RPC client that connects to the VS extension process
/// and proxies IRoslynAnalysisRpc calls.
/// </summary>
public sealed class RpcClient(CancellationTokenSource shutdownCts) : IRoslynAnalysisRpc, IServerRpc, IDisposable
{
	private NamedPipeClientStream? _pipeClient;
    private JsonRpc? _jsonRpc;
    private IRoslynAnalysisRpc? _proxy;
    private bool _disposed;

    public bool IsConnected => _pipeClient?.IsConnected ?? false;

    public async Task ConnectAsync(string pipeName, int timeoutMs = 15000)
    {
        _pipeClient = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await _pipeClient.ConnectAsync(timeoutMs);
        _jsonRpc = JsonRpc.Attach(_pipeClient, this);
        _proxy = _jsonRpc.Attach<IRoslynAnalysisRpc>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _jsonRpc?.Dispose();
        _pipeClient?.Dispose();
    }

    private IRoslynAnalysisRpc Proxy =>
        _proxy ?? throw new InvalidOperationException("Not connected to Visual Studio");

    // IServerRpc
    public Task ShutdownAsync()
    {
        Console.Error.WriteLine("Shutdown requested via RPC");
        shutdownCts.Cancel();
        return Task.CompletedTask;
    }

    // IRoslynAnalysisRpc proxy methods
    public Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers)
        => Proxy.ValidateFileAsync(filePath, includeWarnings, runAnalyzers);

    public Task<FindReferencesResult> FindReferencesAsync(string filePath, int line, int column, int maxResults)
        => Proxy.FindReferencesAsync(filePath, line, column, maxResults);

    public Task<GoToDefinitionResult> GoToDefinitionAsync(string filePath, int line, int column)
        => Proxy.GoToDefinitionAsync(filePath, line, column);

    public Task<List<DocumentSymbolInfo>> GetDocumentSymbolsAsync(string filePath)
        => Proxy.GetDocumentSymbolsAsync(filePath);

    public Task<SearchSymbolsResult> SearchSymbolsAsync(string query, int maxResults)
        => Proxy.SearchSymbolsAsync(query, maxResults);

    public Task<DeadCodeAnalysisResult> FindDeadCodeAsync(int maxResults, bool includeInternal, bool includePublic)
        => Proxy.FindDeadCodeAsync(maxResults, includeInternal, includePublic);

    public Task<MemberLookupResult> GetSymbolInfoAsync(string filePath, int line, int column)
        => Proxy.GetSymbolInfoAsync(filePath, line, column);
}
