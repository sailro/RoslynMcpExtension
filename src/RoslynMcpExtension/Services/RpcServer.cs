using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace RoslynMcpExtension.Services;

public sealed class RpcServer(RoslynAnalysisService analysisService) : IDisposable
{
	private NamedPipeServerStream? _pipeServer;
    private JsonRpc? _jsonRpc;
    private CancellationTokenSource? _cts;
    private string? _pipeName;
    private bool _disposed;

    public bool IsRunning => _jsonRpc != null && !_disposed;

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
                _pipeServer?.Dispose();
                _pipeServer = null;

                _pipeServer = new NamedPipeServerStream(
                    _pipeName!,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                _jsonRpc = JsonRpc.Attach(_pipeServer, analysisService);
                _jsonRpc.Disconnected += (_, _) =>
                {
                    _jsonRpc = null;
                    _pipeServer?.Dispose();
                    _pipeServer = null;
                };

                await _jsonRpc.Completion;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
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
