using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using RoslynMcpExtension.Services;

namespace RoslynMcpExtension;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("Roslyn MCP Server", "Exposes Roslyn code analysis via MCP", "1.0.0")]
[ProvideOptionPage(typeof(SettingsPage), "Roslyn MCP Server", "General", 0, 0, true)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[Guid("b8a7f3e2-1c4d-4e5f-9a6b-8c7d0e1f2a3b")]
[ProvideMenuResource("Menus.ctmenu", 1)]
public sealed class RoslynMcpPackage : AsyncPackage
{
    public static RoslynMcpPackage? Instance { get; private set; }

    internal RoslynAnalysisService? AnalysisService { get; private set; }
    internal RpcServer? RpcServer { get; private set; }
    internal ServerProcessManager? ProcessManager { get; private set; }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        Instance = this;

        var componentModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
        AnalysisService = componentModel.GetService<RoslynAnalysisService>();
        RpcServer = new RpcServer(AnalysisService);
        ProcessManager = new ServerProcessManager(this);

        await ServerCommands.InitializeAsync(this);

        var settings = (SettingsPage)GetDialogPage(typeof(SettingsPage));
        if (settings.AutoStart)
        {
            _ = Task.Run(async () =>
            {
                var pipeName = RpcServer.Start();
                await ProcessManager.StartAsync(pipeName, settings.Port, settings.ServerName);
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ProcessManager?.StopAsync().GetAwaiter().GetResult();
            RpcServer?.Dispose();
            Instance = null;
        }
        base.Dispose(disposing);
    }
}
