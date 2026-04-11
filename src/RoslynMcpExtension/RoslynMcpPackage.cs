using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using RoslynMcpExtension.Services;

namespace RoslynMcpExtension;

[ProvideBindingPath]
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("Roslyn MCP Extension", "Exposes Roslyn code analysis via MCP", "1.0.0")]
[ProvideOptionPage(typeof(SettingsPage), "Roslyn MCP Extension", "General", 0, 0, true)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[Guid("b8a7f3e2-1c4d-4e5f-9a6b-8c7d0e1f2a3b")]
[ProvideMenuResource("Menus.ctmenu", 1)]
public sealed class RoslynMcpPackage : AsyncPackage
{
    public static RoslynMcpPackage? Instance { get; private set; }

    internal OutputLogger? Logger { get; private set; }
    internal RoslynAnalysisService? AnalysisService { get; private set; }
    internal RpcServer? RpcServer { get; private set; }
    internal ServerProcessManager? ProcessManager { get; private set; }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        Instance = this;

        try
        {
            Logger = OutputLogger.Create();
            Logger?.Log("Extension loading...");

            if (await GetServiceAsync(typeof(SComponentModel)) is not IComponentModel componentModel)
            {
                Logger?.Log("Failed to obtain IComponentModel service");
                return;
            }

            AnalysisService = componentModel.GetService<RoslynAnalysisService>();
            AnalysisService.Logger = Logger;

            RpcServer = new RpcServer(AnalysisService, Logger);
            ProcessManager = new ServerProcessManager(Logger);

            await ServerCommands.InitializeAsync(this);

            var settings = (SettingsPage)GetDialogPage(typeof(SettingsPage));
            if (settings.AutoStart)
            {
                Logger?.Log("Auto-starting MCP server...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pipeName = RpcServer.Start();
                        await ProcessManager.StartAsync(pipeName, settings.Port, settings.ServerName);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log($"Auto-start failed: {ex.Message}");
                    }
                }, cancellationToken);
            }

            Logger?.Log("Extension loaded");
        }
        catch (Exception ex)
        {
            Logger?.Log($"Extension initialization failed: {ex.Message}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger?.Log("Extension shutting down...");
            _ = ProcessManager?.StopAsync();
            RpcServer?.Dispose();
            Instance = null;
        }
        base.Dispose(disposing);
    }
}
