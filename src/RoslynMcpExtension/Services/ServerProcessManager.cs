using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace RoslynMcpExtension.Services;

internal sealed class ServerProcessManager(OutputLogger? logger)
{
    private Process? _serverProcess;
    public bool IsRunning => _serverProcess is { HasExited: false };

    public async Task StartAsync(string pipeName, int port, string serverName)
    {
	    if (IsRunning)
		    return;

	    var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
	    var serverDir = Path.Combine(extensionDir!, "McpServer");
	    var serverPath = Path.Combine(serverDir, "RoslynMcpExtension.Server.dll");

	    if (!File.Exists(serverPath))
	    {
		    logger?.Log($"Server executable not found at expected path: {serverPath}");
		    return;
	    }

		_serverProcess = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"\"{serverPath}\" --pipe \"{pipeName}\" --port {port} --name \"{serverName}\"",
				WorkingDirectory = serverDir,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			},
			EnableRaisingEvents = true
		};
		_serverProcess.Exited += (_, _) =>
	    {
		    logger?.Log($"MCP Server process exited with code {_serverProcess?.ExitCode ?? -1}");
		    _serverProcess = null;
	    };

	    _serverProcess.ErrorDataReceived += (_, e) =>
	    {
		    if (!string.IsNullOrEmpty(e.Data))
			    logger?.Log($"[MCP Server] {e.Data}");
	    };

		_serverProcess.Start();
		_serverProcess.BeginErrorReadLine();

		await Task.Delay(2000);

		if (_serverProcess == null || _serverProcess.HasExited)
		{
			var exitCode = _serverProcess?.ExitCode ?? -1;
			_serverProcess?.Dispose();
			_serverProcess = null;
			logger?.Log($"MCP Server failed to start (exit code {exitCode}). Port {port} may already be in use.");
			return;
		}

		logger?.Log($"MCP Server started on http://localhost:{port}/mcp (pipe: {pipeName})");
    }

    public async Task StopAsync()
    {
        if (_serverProcess == null) return;

        logger?.Log("Stopping MCP Server...");

        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill();
                _serverProcess.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            logger?.Log($"Error stopping server: {ex.Message}");
        }
        finally
        {
            _serverProcess?.Dispose();
            _serverProcess = null;
        }

        logger?.Log("MCP Server stopped");
        await Task.CompletedTask;
    }
}
