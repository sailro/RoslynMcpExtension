using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace RoslynMcpExtension;

internal static class ServerCommands
{
    private static readonly Guid CommandSet = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private const int StartCommandId = 0x0100;
    private const int StopCommandId = 0x0101;

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService) 
	        return;

        commandService.AddCommand(new MenuCommand(OnStartServer, new CommandID(CommandSet, StartCommandId)));
        commandService.AddCommand(new MenuCommand(OnStopServer, new CommandID(CommandSet, StopCommandId)));
    }

    private static void OnStartServer(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var pkg = RoslynMcpPackage.Instance;
        if (pkg?.RpcServer == null || pkg.ProcessManager == null) return;

        var settings = (SettingsPage)pkg.GetDialogPage(typeof(SettingsPage));
        _ = Task.Run(async () =>
        {
            var pipeName = pkg.RpcServer.Start();
            await pkg.ProcessManager.StartAsync(pipeName, settings.Port, settings.ServerName);
        });
    }

    private static void OnStopServer(object sender, EventArgs e)
    {
        var pkg = RoslynMcpPackage.Instance;
        if (pkg?.ProcessManager == null) return;

        _ = Task.Run(async () => await pkg.ProcessManager.StopAsync());
    }
}
