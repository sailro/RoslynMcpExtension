using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using RoslynMcpExtension.Shared;
using StreamJsonRpc;

namespace RoslynMcpExtension.Services;

/// <summary>
/// Named pipe JSON-RPC server that runs inside the VS extension process.
/// Delegates incoming RPC calls to the RoslynAnalysisService.
/// </summary>
public sealed class RpcServer : IDisposable
{
    private readonly RoslynAnalysisService _analysisService;
    private NamedPipeServerStream? _pipeServer;
    private JsonRpc? _jsonRpc;
    private CancellationTokenSource? _cts;
    private string? _pipeName;
    private bool _disposed;

    public bool IsRunning => _jsonRpc != null && !_disposed;
    public string? PipeName => _pipeName;

    public RpcServer(RoslynAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>
    /// Starts the named pipe server and returns the pipe name.
    /// </summary>
    public string Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RpcServer));
        if (IsRunning) return _pipeName!;

        _pipeName = $"RoslynMcp_{Guid.NewGuid():N}";
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => ListenAsync(_cts.Token));

        return _pipeName;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    _pipeName!,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                _jsonRpc = JsonRpc.Attach(_pipeServer, _analysisService);
                _jsonRpc.Disconnected += (s, e) =>
                {
                    _jsonRpc = null;
                    _pipeServer?.Dispose();
                    _pipeServer = null;
                };

                // Wait for disconnect before accepting a new connection
                await _jsonRpc.Completion;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Brief delay before retry
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _jsonRpc?.Dispose();
        _pipeServer?.Dispose();
    }
}
