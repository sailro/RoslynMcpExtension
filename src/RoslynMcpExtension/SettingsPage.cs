using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace RoslynMcpExtension;

[ComVisible(true)]
public class SettingsPage : DialogPage
{
    [System.ComponentModel.Category("Server")]
    [System.ComponentModel.DisplayName("Port")]
    [System.ComponentModel.Description("HTTP port for the MCP server (default: 5050)")]
    public int Port { get; set; } = 5050;

    [System.ComponentModel.Category("Server")]
    [System.ComponentModel.DisplayName("Server Name")]
    [System.ComponentModel.Description("Server name displayed to MCP clients")]
    public string ServerName { get; set; } = "Roslyn MCP Server";

    [System.ComponentModel.Category("Server")]
    [System.ComponentModel.DisplayName("Auto Start")]
    [System.ComponentModel.Description("Automatically start the MCP server when a solution is loaded")]
    public bool AutoStart { get; set; } = true;
}
