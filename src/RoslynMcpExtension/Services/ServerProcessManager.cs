using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace RoslynMcpExtension.Services;

public sealed class ServerProcessManager()
{
    private Process? _serverProcess;
    public bool IsRunning => _serverProcess is { HasExited: false };

    public async Task StartAsync(string pipeName, int port, string serverName)
    {
	    if (IsRunning)
		    return;

	    var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
	    var serverPath = Path.Combine(extensionDir!, "McpServer", "RoslynMcpExtension.Server.dll");

	    if (!File.Exists(serverPath))
	    {
		    await WriteOutputAsync($"Server executable not found at expected path: {serverPath}");
		    return;
	    }

		_serverProcess = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"\"{serverPath}\" --pipe \"{pipeName}\" --port {port} --name \"{serverName}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			},
			EnableRaisingEvents = true
		};
		_serverProcess.Exited += (s, e) =>
	    {
		    _ = WriteOutputAsync($"MCP Server process exited with code {_serverProcess?.ExitCode ?? -1}");
		    _serverProcess = null;
	    };

	    _serverProcess.ErrorDataReceived += (s, e) =>
	    {
		    if (!string.IsNullOrEmpty(e.Data))
			    _ = WriteOutputAsync($"[MCP Server] {e.Data}");
	    };

		_serverProcess.Start();
		_serverProcess.BeginErrorReadLine();

		await Task.Delay(2000);

		if (_serverProcess == null || _serverProcess.HasExited)
		{
			var exitCode = _serverProcess?.ExitCode ?? -1;
			_serverProcess?.Dispose();
			_serverProcess = null;
			await WriteOutputAsync($"MCP Server failed to start (exit code {exitCode}). Port {port} may already be in use.");
			return;
		}

		await WriteOutputAsync($"MCP Server started on http://localhost:{port} (pipe: {pipeName})");
    }

    public async Task StopAsync()
    {
        if (_serverProcess == null) return;

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
            await WriteOutputAsync($"Error stopping server: {ex.Message}");
        }
        finally
        {
            _serverProcess?.Dispose();
            _serverProcess = null;
        }

        await WriteOutputAsync("MCP Server stopped");
    }

    private static Task WriteOutputAsync(string message)
    {
        Debug.WriteLine(message);
        return Task.CompletedTask;
    }
}
