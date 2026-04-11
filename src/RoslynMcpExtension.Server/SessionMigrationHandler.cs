using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace RoslynMcpExtension.Server;

/// <summary>
/// Allows clients with stale session IDs (e.g. after a server restart) to
/// seamlessly re-establish a session without requiring a full re-initialization.
/// </summary>
internal sealed class SessionMigrationHandler(RpcClient rpcClient) : ISessionMigrationHandler
{
    private InitializeRequestParams? _lastInitParams;

    public ValueTask OnSessionInitializedAsync(
        HttpContext context,
        string sessionId,
        InitializeRequestParams initializeParams,
        CancellationToken cancellationToken)
    {
        _lastInitParams = initializeParams;
        return ValueTask.CompletedTask;
    }

    public async ValueTask<InitializeRequestParams?> AllowSessionMigrationAsync(
        HttpContext context,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await rpcClient.LogAsync($"Migrating stale session: {sessionId}");
        // Return last known init params so the SDK recreates the session transparently.
        // For a single-instance local server this is always valid.
        return _lastInitParams ?? new InitializeRequestParams
        {
            ProtocolVersion = "2025-03-26",
            ClientInfo = new Implementation { Name = "unknown", Version = "0.0.0" },
            Capabilities = new ClientCapabilities()
        };
    }
}
