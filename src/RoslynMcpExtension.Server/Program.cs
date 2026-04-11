using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
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
    RpcClient? rpcClient = null;

    try
    {
        rpcClient = new RpcClient(shutdownCts);
        await rpcClient.ConnectAsync(pipeName);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to connect to Visual Studio via pipe '{pipeName}': {ex.Message}");
        rpcClient?.Dispose();
        return;
    }

    await rpcClient.LogAsync($"Connected to Visual Studio via pipe: {pipeName}");

    WebApplication app;
    try
    {
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
        builder.Services.AddSingleton<ISessionMigrationHandler>(sp => new SessionMigrationHandler(sp.GetRequiredService<RpcClient>()));

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
                // Enable legacy SSE endpoints (/sse + /message) so existing clients
                // that were configured with http://localhost:5050/sse keep working.
#pragma warning disable MCP9004 // EnableLegacySse is obsolete
                options.EnableLegacySse = true;
#pragma warning restore MCP9004
            })
            .WithTools<ValidateFileTool>()
            .WithTools<FindReferencesTool>()
            .WithTools<GoToDefinitionTool>()
            .WithTools<DocumentSymbolsTool>()
            .WithTools<SearchSymbolsTool>()
            .WithTools<DeadCodeTool>()
            .WithTools<SymbolInfoTool>();

        app = builder.Build();
    }
    catch (Exception ex)
    {
        await rpcClient.LogAsync($"Failed to build HTTP server: {ex.Message}");
        rpcClient.Dispose();
        return;
    }

    try
    {
        app.MapMcp("/").AllowAnonymous();
        app.MapMcp("/mcp").AllowAnonymous();

        // RFC 9728: serve protected resource metadata with no authorization_servers
        // to tell MCP clients (e.g. Copilot) this server is public and needs no OAuth.
        app.MapGet("/.well-known/oauth-protected-resource", (HttpContext ctx) =>
            Microsoft.AspNetCore.Http.Results.Json(new
            {
                resource = $"{ctx.Request.Scheme}://{ctx.Request.Host}",
                authorization_servers = Array.Empty<string>()
            })).AllowAnonymous();

        var bindingUrl = $"http://{host}:{port}";
        app.Urls.Add(bindingUrl);

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        shutdownCts.Token.Register(() => lifetime.StopApplication());

        await rpcClient.LogAsync($"MCP Server listening on {bindingUrl}");
        await rpcClient.LogAsync($"Streamable HTTP: {bindingUrl}/mcp | Legacy SSE: {bindingUrl}/sse");
        await rpcClient.LogAsync("Tools: roslyn_validate_file, roslyn_find_references, roslyn_go_to_definition, roslyn_get_document_symbols, roslyn_search_symbols, roslyn_find_dead_code, roslyn_get_symbol_info");

        await app.RunAsync();
        await rpcClient.LogAsync("Server shutdown complete");
    }
    catch (IOException ex)
    {
        await rpcClient.LogAsync($"HTTP server failed (port {port} may already be in use): {ex.Message}");
    }
    catch (Exception ex)
    {
        await rpcClient.LogAsync($"HTTP server error: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        rpcClient.Dispose();
    }
}
