using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Server;
using RoslynMcpExtension.Server.Tools;

var pipeOption = new Option<string>(
    name: "--pipe",
    description: "Named pipe name for connecting to Visual Studio")
{ IsRequired = true };

var hostOption = new Option<string>(
    name: "--host",
    getDefaultValue: () => "localhost",
    description: "Host address to bind the HTTP server to");

var portOption = new Option<int>(
    name: "--port",
    getDefaultValue: () => 5050,
    description: "HTTP port for the MCP server");

var nameOption = new Option<string>(
    name: "--name",
    getDefaultValue: () => "Roslyn MCP Server",
    description: "Server name displayed to MCP clients");

var logLevelOption = new Option<string>(
    name: "--log-level",
    getDefaultValue: () => "Information",
    description: "Minimum log level (Error, Warning, Information, Debug)");

var rootCommand = new RootCommand("Roslyn MCP Server - Exposes C# code analysis via MCP")
{
    pipeOption, hostOption, portOption, nameOption, logLevelOption
};

rootCommand.SetHandler(async (string pipeName, string host, int port, string serverName, string logLevel) =>
{
    await RunServerAsync(pipeName, host, port, serverName, logLevel);
}, pipeOption, hostOption, portOption, nameOption, logLevelOption);

return await rootCommand.InvokeAsync(args);

static async Task RunServerAsync(string pipeName, string host, int port, string serverName, string logLevel)
{
    var msLogLevel = logLevel switch
    {
        "Error" => LogLevel.Error,
        "Warning" => LogLevel.Warning,
        "Debug" => LogLevel.Debug,
        _ => LogLevel.Information
    };

    using var shutdownCts = new CancellationTokenSource();
    var rpcClient = new RpcClient(shutdownCts);

    Console.Error.WriteLine($"Connecting to Visual Studio via pipe: {pipeName}");
    await rpcClient.ConnectAsync(pipeName);
    Console.Error.WriteLine("Connected to Visual Studio");

    var builder = WebApplication.CreateBuilder();

    builder.Logging.SetMinimumLevel(msLogLevel);
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Logging.AddFilter("ModelContextProtocol", msLogLevel);

    builder.Services.AddSingleton(rpcClient);

    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = serverName,
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithTools<ValidateFileTool>()
    .WithTools<FindReferencesTool>()
    .WithTools<GoToDefinitionTool>()
    .WithTools<DocumentSymbolsTool>()
    .WithTools<SearchSymbolsTool>()
    .WithTools<SymbolInfoTool>()
    .WithTools<ComplexityTool>();

    var app = builder.Build();
    app.MapMcp();

    var bindingUrl = $"http://{host}:{port}";
    app.Urls.Add(bindingUrl);

    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    shutdownCts.Token.Register(() => lifetime.StopApplication());

    Console.Error.WriteLine($"Roslyn MCP Server listening on {bindingUrl}");
    Console.Error.WriteLine($"Tools: roslyn_validate_file, roslyn_find_references, roslyn_go_to_definition, roslyn_get_document_symbols, roslyn_search_symbols, roslyn_get_symbol_info, roslyn_analyze_complexity");

    await app.RunAsync();
    Console.Error.WriteLine("Server shutdown complete");
}
