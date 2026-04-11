using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class ValidateFileTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_validate_file")]
	[Description("Validates a C# file using the live Roslyn workspace. Returns compiler errors, warnings, and optional analyzer diagnostics. The file must be part of the currently open Visual Studio solution.")]
	public Task<ValidateFileResult> ValidateFile(
		[Description("Absolute path to the C# file to validate")] string filePath,
		[Description("Include warnings in output (default: true)")] bool includeWarnings = true,
		[Description("Run code analyzers in addition to compiler diagnostics (default: false)")] bool runAnalyzers = false)
		=> rpc.ValidateFileAsync(filePath, includeWarnings, runAnalyzers);
}
