using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace RoslynMcpExtension.Services;

internal sealed class RpcServer(RoslynAnalysisService analysisService, OutputLogger? logger) : IDisposable
{
	private NamedPipeServerStream? _pipeServer;
    private JsonRpc? _jsonRpc;
    private CancellationTokenSource? _cts;
    private string? _pipeName;
    private bool _disposed;
    private string? _lastError;

    public string Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RpcServer));

        // Tear down any previous listen loop before starting a new one
        Stop();

        _pipeName = $"RoslynMcp_{Guid.NewGuid():N}";
        _cts = new CancellationTokenSource();
        _lastError = null;

        logger?.Log($"RPC server starting on pipe: {_pipeName}");
        _ = Task.Run(() => ListenAsync(_cts.Token));

        return _pipeName;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _jsonRpc?.Dispose();
        _jsonRpc = null;

        _pipeServer?.Dispose();
        _pipeServer = null;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipeServer?.Dispose();
                _pipeServer = null;

                _pipeServer = new NamedPipeServerStream(
                    _pipeName!,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                logger?.Log("RPC client connected");
                _lastError = null;

                _jsonRpc = JsonRpc.Attach(_pipeServer, analysisService);
                _jsonRpc.Disconnected += (_, _) =>
                {
                    logger?.Log("RPC client disconnected");
                    _jsonRpc = null;
                    _pipeServer?.Dispose();
                    _pipeServer = null;
                };

#pragma warning disable VSTHRD003 // Completion is a long-running task representing the RPC lifetime
                await _jsonRpc.Completion;
#pragma warning restore VSTHRD003
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Deduplicate repeated errors to avoid spamming the output pane
                if (ex.Message != _lastError)
                {
                    logger?.Log($"RPC server error: {ex.Message}");
                    _lastError = ex.Message;
                }

                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        logger?.Log("RPC server stopping");
        Stop();
    }
}
