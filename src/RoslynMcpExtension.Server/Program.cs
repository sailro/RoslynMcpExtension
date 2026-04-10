using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using RoslynMcpExtension.Server;
using RoslynMcpExtension.Server.Tools;

var pipeOption = new Option<string>("--pipe")
{
    Description = "Named pipe name for connecting to Visual Studio",
    Required = true
};

var hostOption = new Option<string>("--host")
{
    Description = "Host address to bind the HTTP server to",
    DefaultValueFactory = _ => "localhost"
};

var portOption = new Option<int>("--port")
{
    Description = "HTTP port for the MCP server",
    DefaultValueFactory = _ => 5050
};

var nameOption = new Option<string>("--name")
{
    Description = "Server name displayed to MCP clients",
    DefaultValueFactory = _ => "Roslyn MCP Server"
};

var logLevelOption = new Option<string>("--log-level")
{
    Description = "Minimum log level (Error, Warning, Information, Debug)",
    DefaultValueFactory = _ => "Information"
};

var rootCommand = new RootCommand("Roslyn MCP Server - Exposes C# code analysis via MCP")
{
    pipeOption, hostOption, portOption, nameOption, logLevelOption
};

rootCommand.SetAction(async (parseResult, _) =>
{
    var pipeName = parseResult.GetValue(pipeOption)!;
    var host = parseResult.GetValue(hostOption)!;
    var port = parseResult.GetValue(portOption);
    var serverName = parseResult.GetValue(nameOption)!;
    var logLevel = parseResult.GetValue(logLevelOption)!;
    await RunServerAsync(pipeName, host, port, serverName, logLevel);
});

return await rootCommand.Parse(args).InvokeAsync();

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

    var serverDirectory = AppContext.BaseDirectory;
    Directory.SetCurrentDirectory(serverDirectory);

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ContentRootPath = serverDirectory
    });

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
        .WithHttpTransport(options =>
        {
            // This server only exposes request/response tools and does not rely on
            // server-push features, so stateless transport avoids stale session
            // failures across reconnects and server restarts.
            options.Stateless = true;
        })
        .WithTools<ValidateFileTool>()
        .WithTools<FindReferencesTool>()
        .WithTools<GoToDefinitionTool>()
        .WithTools<DocumentSymbolsTool>()
        .WithTools<SearchSymbolsTool>()
        .WithTools<DeadCodeTool>()
        .WithTools<SymbolInfoTool>();

    var app = builder.Build();

    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/" || context.Request.Path.StartsWithSegments("/mcp"))
        {
            // Some MCP clients keep sending a cached session header even when the
            // server is stateless. Strip it so reconnects after restarts still work.
            context.Request.Headers.Remove("Mcp-Session-Id");
            context.Request.Headers.Remove("mcp-session-id");
        }

        await next();
    });

    app.MapMcp("/");
    app.MapMcp("/mcp");

    var bindingUrl = $"http://{host}:{port}";
    app.Urls.Add(bindingUrl);

    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    shutdownCts.Token.Register(() => lifetime.StopApplication());

    Console.Error.WriteLine($"Roslyn MCP Server listening on {bindingUrl}");
    Console.Error.WriteLine($"Streamable HTTP endpoint: {bindingUrl}/mcp (stateless)");
    Console.Error.WriteLine("Tools: roslyn_validate_file, roslyn_find_references, roslyn_go_to_definition, roslyn_get_document_symbols, roslyn_search_symbols, roslyn_find_dead_code, roslyn_get_symbol_info");

    await app.RunAsync();
    Console.Error.WriteLine("Server shutdown complete");
}
