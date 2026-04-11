using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class ValidateFileTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_validate_file")]
	[Description("Validates a C# file and returns compiler errors, warnings, and optional analyzer diagnostics.")]
	public Task<ValidateFileResult> ValidateFile(
		[Description("Absolute path to the C# file to validate")] string filePath,
		[Description("Include warnings in output (default: true)")] bool includeWarnings = true,
		[Description("Run code analyzers in addition to compiler diagnostics (default: false)")] bool runAnalyzers = false)
		=> rpc.ValidateFileAsync(filePath, includeWarnings, runAnalyzers);
}
